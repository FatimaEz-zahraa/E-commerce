// Services/Implementations/RagService.cs - VERSION OPTIMISÉE AVEC VECTOR SEARCH
using E_commerce.Models.DTOs;
using E_commerce.Services.Interfaces;
using E_commerce.Services.Rag;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace E_commerce.Services.Implementations
{
    public class RagService : IRagService
    {
        private readonly ProductKnowledgeService _knowledge;
        private readonly GeminiService _llm;
        private readonly ILogger<RagService> _logger;
        private readonly IProductService _productService;
        private readonly IMemoryCache _memoryCache;
        private readonly VectorProductIndexService _vectorIndex;

        public RagService(
            ProductKnowledgeService knowledge,
            GeminiService llm,
            ILogger<RagService> logger,
            IProductService productService,
            IMemoryCache memoryCache,
            VectorProductIndexService vectorIndex)
        {
            _knowledge = knowledge;
            _llm = llm;
            _logger = logger;
            _productService = productService;
            _memoryCache = memoryCache;
            _vectorIndex = vectorIndex;
        }

        /// <summary>
        /// Question simple au système RAG
        /// </summary>
        public async Task<string> AskAsync(string question)
        {
            return await AskWithProductContextAsync(question);
        }

        /// <summary>
        /// Question avec contexte produit et recommandations structurées
        /// </summary>
        public async Task<AssistantResponse> AskWithProductsAsync(string question)
        {
            try
            {
                var cacheKey = $"rag_structured_{question.ToLower().GetHashCode()}";

                // Vérifier le cache
                if (_memoryCache.TryGetValue(cacheKey, out AssistantResponse cachedResponse))
                {
                    _logger.LogInformation("Réponse récupérée du cache");
                    return cachedResponse;
                }

                // 1. Obtenir la réponse textuelle (formatée pour recommandations)
                var textResponse = await GetProductRecommendationsAsync(question);

                // 2. Obtenir les produits recommandés
                var recommendedProducts = await GetRecommendedProductsAsync(question);

                // 3. Construire la réponse structurée
                var response = new AssistantResponse
                {
                    TextResponse = textResponse,
                    RecommendedProducts = recommendedProducts,
                    SearchQuery = question,
                    Timestamp = DateTime.UtcNow,
                    ProductCount = recommendedProducts.Count,
                    HasProducts = recommendedProducts.Any()
                };

                // Attribuer un identifiant unique au message pour rattacher les cartes côté client
                response.MessageId = Guid.NewGuid().ToString();

                // Mettre en cache (10 minutes)
                _memoryCache.Set(cacheKey, response, TimeSpan.FromMinutes(10));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans AskWithProductsAsync");
                return new AssistantResponse
                {
                    TextResponse = "Désolé, je rencontre des difficultés techniques.",
                    RecommendedProducts = new List<ProductDto>(),
                    SearchQuery = question,
                    Timestamp = DateTime.UtcNow,
                    HasProducts = false
                };
            }
        }

        /// <summary>
        /// Question avec contexte produit spécifique
        /// </summary>
        public async Task<string> AskWithProductContextAsync(string question, ProductDto? currentProduct = null)
        {
            try
            {
                var cacheKey = $"rag_{question.ToLower().GetHashCode()}_{currentProduct?.Id ?? Guid.Empty}";

                // Vérifier le cache
                if (_memoryCache.TryGetValue(cacheKey, out string cachedText))
                {
                    return cachedText;
                }

                // 1. Obtenir le contexte depuis ProductKnowledgeService
                var knowledgeContext = await _knowledge.GetRelevantContextAsync(question, currentProduct);

                // 2. Obtenir les produits recommandés
                var recommendedProducts = await GetRecommendedProductsAsync(question, currentProduct?.Id);

                // 3. Construire le contexte enrichi
                var enhancedContext = BuildEnhancedContext(knowledgeContext, recommendedProducts, question);

                // 4. Créer le prompt système optimisé
                var systemPrompt = @"
Tu es un assistant commercial expert pour E-Shop, spécialisé dans les produits high-tech.

DIRECTIVES IMPORTANTES :
✅ Analyse le contexte fourni de notre base de données
✅ Liste TOUS les produits spécifiques fournis dans 'PRODUITS SÉLECTIONNÉS' avec leurs noms EXACTS
✅ Présente-les dans le MÊME ORDRE que dans le contexte
✅ Sois enthousiaste mais honnête
✅ Personnalise tes réponses selon la demande
✅ Termine par une question pour aider davantage
✅ Utilise des emojis pour rendre la conversation plus vivante

STYLE : Professionnel, amical, expert

INTERDICTIONS :
❌ Ne pas inventer de produits
❌ Ne pas donner de fausses informations
❌ Ne pas être trop technique sauf si demandé
❌ Ne pas omettre des produits de la liste fournie
";

                var userPrompt = $@"
## CONTEXTE DE NOTRE CATALOGUE ##

{enhancedContext}

## QUESTION DU CLIENT ##
""{question}""

## INSTRUCTIONS ##
1. Analyse le contexte ci-dessus
2. Réponds de manière personnalisée
3. Liste TOUS les produits de la section 'PRODUITS SÉLECTIONNÉS' avec leur nom exact et prix
4. Respecte l'ordre de la liste fournie
5. Donne des conseils d'achat utiles
6. Termine par une question pour continuer la conversation

Sois concis mais complet (max 200 mots).
";

                _logger.LogInformation("Question RAG: {Question}", TruncateText(question, 50));

                // 5. Appeler le LLM
                var response = await _llm.AskAsync(systemPrompt, userPrompt);

                // Mettre en cache (10 minutes)
                _memoryCache.Set(cacheKey, response, TimeSpan.FromMinutes(10));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans RagService.AskWithProductContextAsync");
                return "Je rencontre des difficultés techniques. Pouvez-vous reformuler votre question ?";
            }
        }

        /// <summary>
        /// Génère des recommandations de produits formatées
        /// </summary>
        public async Task<string> GetProductRecommendationsAsync(string query)
        {
            try
            {
                var products = await GetRecommendedProductsAsync(query);

                if (!products.Any())
                {
                    return await GetFallbackRecommendation(query);
                }

                var systemPrompt = @"
Tu es un conseiller commercial expert. Présente les produits de manière attractive et persuasive.

FORMAT ATTENDU :
1. **Introduction** : Résumé de ce qui correspond (1 phrase)
2. **Produits recommandés** (max 3) :
   • Nom et marque
   • Points forts principaux
   • Prix et notation
3. **Conseil final** : Recommandation personnalisée
4. **Question** : Pour affiner la recherche

TON : Enthousiaste, professionnel, convaincant
LONGUEUR : 150-200 mots maximum
";

                var context = BuildProductContext(products.Take(3).ToList());

                var userPrompt = $@"
## PRODUITS DISPONIBLES ##

{context}

## DEMANDE DU CLIENT ##
""{query}""

## TÂCHE ##
Présente ces produits de manière convaincante en suivant le format attendu.
";

                return await _llm.AskAsync(systemPrompt, userPrompt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération de recommandations");
                return "Je ne peux pas générer de recommandations pour le moment. Consultez notre catalogue !";
            }
        }

        /// <summary>
        /// Extrait les mots-clés d'une requête utilisateur
        /// </summary>
        public async Task<List<string>> ExtractSearchKeywordsAsync(string userQuery)
        {
            try
            {
                var cacheKey = $"keywords_{userQuery.ToLower().GetHashCode()}";

                if (_memoryCache.TryGetValue(cacheKey, out List<string> cached))
                {
                    return cached;
                }

                var systemPrompt = @"
Extrait UNIQUEMENT les mots-clés importants pour une recherche e-commerce.
Retourne les mots-clés séparés par des virgules.

GARDE : marques, catégories, caractéristiques, adjectifs importants
EXCLUS : articles, prépositions, verbes génériques

Exemple : ""Je cherche un smartphone Samsung pas cher"" → ""smartphone, Samsung, pas cher""
";

                var response = await _llm.AskAsync(systemPrompt, $"Extrait les mots-clés de : \"{userQuery}\"");

                var keywords = response.Split(',')
                    .Select(k => k.Trim().ToLower())
                    .Where(k => k.Length > 2)
                    .Distinct()
                    .Take(7)
                    .ToList();

                _memoryCache.Set(cacheKey, keywords, TimeSpan.FromHours(1));

                return keywords;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erreur extraction mots-clés, fallback simple");
                return SimpleKeywordExtraction(userQuery);
            }
        }

        /// <summary>
        /// Génère une description produit enrichie par IA
        /// </summary>
        public async Task<string> GenerateEnhancedProductDescriptionAsync(ProductDto product)
        {
            if (product == null) return "Produit non disponible.";

            try
            {
                var systemPrompt = @"
Tu es un expert en copywriting e-commerce.
Génère une description produit optimisée et persuasive.

CRITÈRES :
✅ Attrayante et professionnelle
✅ Met en avant les bénéfices client
✅ Structure claire avec emojis
✅ Appel à l'action
✅ 150-250 mots

TON : Professionnel, enthousiaste, convaincant
";

                var userPrompt = $@"
## INFORMATIONS PRODUIT ##

Nom : {product.Name}
Marque : {product.Brand}
Catégorie : {product.Category}
Prix : {product.Price:C}
Note : {product.Rating:F1}/5 ({product.ReviewCount} avis)
Description actuelle : {product.Description}

## TÂCHE ##
Génère une description marketing optimisée pour ce produit.
";

                return await _llm.AskAsync(systemPrompt, userPrompt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur génération description");
                return product.Description ?? "Description non disponible.";
            }
        }

        /// <summary>
        /// Compare plusieurs produits
        /// </summary>
        public async Task<string> CompareProductsAsync(List<ProductDto> products)
        {
            if (products == null || products.Count < 2)
            {
                return "Veuillez fournir au moins 2 produits à comparer.";
            }

            var systemPrompt = @"
Tu es un expert en comparaison de produits high-tech.
Compare les produits de manière objective et utile.

FORMAT :
1. **Vue d'ensemble** : Comparaison rapide
2. **Détails comparatifs** :
   - Prix et rapport qualité/prix
   - Caractéristiques principales
   - Notes et avis clients
3. **Recommandations** : Quel produit pour quel profil
4. **Conclusion** : Verdict final

TON : Objectif, informatif, utile
LONGUEUR : 200-300 mots
";

            var productsInfo = string.Join("\n\n", products.Select((p, i) =>
                $@"**Produit {i + 1}** : {p.Name} ({p.Brand})
   Prix : {p.Price:C}
   Note : {p.Rating:F1}/5 ({p.ReviewCount} avis)
   Catégorie : {p.Category}
   Description : {TruncateText(p.Description, 100)}"));

            var userPrompt = $@"
## PRODUITS À COMPARER ##

{productsInfo}

## TÂCHE ##
Fais une comparaison détaillée et objective de ces produits.
";

            return await _llm.AskAsync(systemPrompt, userPrompt);
        }

        /// <summary>
        /// Récupère les analytics du service RAG
        /// </summary>
        public async Task<Dictionary<string, object>> GetAnalyticsAsync()
        {
            try
            {
                var allProducts = await GetAllProductsCachedAsync();
                var categories = GetUniqueCategories(allProducts);

                return new Dictionary<string, object>
                {
                    ["totalProducts"] = allProducts.Count,
                    ["totalCategories"] = categories.Count,
                    ["avgRating"] = allProducts.Any() ? allProducts.Average(p => p.Rating) : 0,
                    ["avgPrice"] = allProducts.Any() ? allProducts.Average(p => p.Price) : 0,
                    ["topCategories"] = categories.Take(5).ToList(),
                    ["cacheStatus"] = "active",
                    ["timestamp"] = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur analytics");
                return new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["timestamp"] = DateTime.UtcNow
                };
            }
        }

        // ==================== MÉTHODES PRIVÉES ====================

        private async Task<List<ProductDto>> GetRecommendedProductsAsync(string query, Guid? excludeId = null)
        {
            try
            {
                var allProducts = await GetAllProductsCachedAsync();
                
                // HYBRID SEARCH: Vector (Semantic) + Keyword (Exact Match)
                List<ProductDto> scoredProducts;

                if (_vectorIndex.IsIndexReady)
                {
                    // 1. Recherche vectorielle sémantique
                    var vectorResults = await _vectorIndex.SearchAsync(query, topK: 20);
                    var vectorScores = vectorResults.ToDictionary(r => r.ProductId, r => r.SemanticScore);

                    // 2. Extraction de mots-clés pour score de correspondance exacte
                    var keywords = await ExtractSearchKeywordsAsync(query);

                    // 3. Combiner les deux scores (Hybrid Scoring)
                    var scoredResults = allProducts
                        .Where(p => !excludeId.HasValue || p.Id != excludeId.Value)
                        .Select(p => new
                        {
                            Product = p,
                            // Score hybride : 70% sémantique + 25% mots-clés + 5% rating (normalisé)
                            SemanticScore = vectorScores.GetValueOrDefault(p.Id, 0f),
                            KeywordScore = CalculateKeywordScore(p, keywords),
                            RatingBonus = (float)p.Rating / 5.0f * 0.05f, // Normalisé 0-1 puis * 0.05 = max 0.05
                            HybridScore = (vectorScores.GetValueOrDefault(p.Id, 0f) * 0.7f) +
                                        (CalculateKeywordScore(p, keywords) * 0.25f) +
                                        ((float)p.Rating / 5.0f * 0.05f) // Rating contribue max 5%
                        })
                        .Where(x => x.SemanticScore > 0.25f || x.KeywordScore > 0.3f) // Seuils cohérents avec le filtrage vectoriel
                        .OrderByDescending(x => x.HybridScore)
                        .ToList();

                    // Log des meilleurs résultats pour debug
                    var topResults = scoredResults.Take(3).ToList();
                    foreach (var result in topResults)
                    {
                        _logger.LogDebug("🎯 {Name}: Semantic={Semantic:F3}, Keyword={Keyword:F3}, Rating={Rating:F3}, Total={Total:F3}",
                            result.Product.Name, result.SemanticScore, result.KeywordScore, result.RatingBonus, result.HybridScore);
                    }

                    scoredProducts = scoredResults
                        .Take(6)
                        .Select(x => x.Product)
                        .ToList();

                    _logger.LogInformation("🔍 Recherche hybride (Vector+Keyword): {Count} résultats pour '{Query}'", scoredProducts.Count, query);
                }
                else
                {
                    // Fallback: recherche par mots-clés uniquement
                    _logger.LogWarning("Index vectoriel non prêt, utilisation de la recherche par mots-clés");
                    var keywords = await ExtractSearchKeywordsAsync(query);

                    scoredProducts = allProducts
                        .Where(p => !excludeId.HasValue || p.Id != excludeId.Value)
                        .Select(p => new
                        {
                            Product = p,
                            Score = CalculateRelevanceScore(p, keywords)
                        })
                        .Where(x => x.Score > 0)
                        .OrderByDescending(x => x.Score)
                        .ThenByDescending(x => x.Product.Rating)
                        .Take(6)
                        .Select(x => x.Product)
                        .ToList();
                }

                // Fallback : produits populaires
                if (!scoredProducts.Any())
                {
                    _logger.LogInformation("Aucun résultat pertinent, affichage des produits populaires");
                    scoredProducts = allProducts
                        .OrderByDescending(p => p.Rating)
                        .ThenByDescending(p => p.ReviewCount)
                        .Take(3)
                        .ToList();
                }

                return scoredProducts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur recherche produits");
                return new List<ProductDto>();
            }
        }

        private double CalculateRelevanceScore(ProductDto product, List<string> keywords)
        {
            double score = 0.0;

            var searchText = $"{product.Name} {product.Brand} {product.Category} {product.Description}".ToLower();

            foreach (var keyword in keywords)
            {
                if (product.Name?.ToLower().Contains(keyword) == true) score += 3.0;
                if (product.Brand?.ToLower().Contains(keyword) == true) score += 2.0;
                if (product.Category?.ToLower().Contains(keyword) == true) score += 2.0;
                if (product.Description?.ToLower().Contains(keyword) == true) score += 1.0;
            }

            // Bonus pour les produits bien notés
            score += (double)product.Rating * 0.2;

            return score;
        }

        /// <summary>
        /// Score de correspondance par mots-clés (normalisé 0-1)
        /// </summary>
        private float CalculateKeywordScore(ProductDto product, List<string> keywords)
        {
            if (!keywords.Any()) return 0f;

            float score = 0f;
            int maxPossibleScore = keywords.Count * 3; // Score max si tous les mots-clés dans le nom

            foreach (var keyword in keywords)
            {
                if (product.Name?.ToLower().Contains(keyword) == true) score += 3f;
                else if (product.Brand?.ToLower().Contains(keyword) == true) score += 2f;
                else if (product.Category?.ToLower().Contains(keyword) == true) score += 2f;
                else if (product.Description?.ToLower().Contains(keyword) == true) score += 1f;
            }

            // Normaliser entre 0 et 1
            return maxPossibleScore > 0 ? score / maxPossibleScore : 0f;
        }

        private string BuildEnhancedContext(string knowledgeContext, List<ProductDto> products, string query)
        {
            var sb = new StringBuilder();
            sb.AppendLine(knowledgeContext);

            if (products.Any())
            {
                sb.AppendLine("\n**PRODUITS SÉLECTIONNÉS POUR CETTE REQUÊTE**");
                foreach (var product in products.Take(5))
                {
                    sb.AppendLine($"• {product.Name} ({product.Brand}) - {product.Price:C}");
                    sb.AppendLine($"  Note: {product.Rating:F1}/5 ({product.ReviewCount} avis)");
                }
            }

            return sb.ToString();
        }

        private string BuildProductContext(List<ProductDto> products)
        {
            return string.Join("\n\n", products.Select((p, i) =>
                $@"**Produit {i + 1}**
   Nom : {p.Name}
   Marque : {p.Brand}
   Prix : {p.Price:C}
   Note : {p.Rating:F1}/5 ({p.ReviewCount} avis)
   Catégorie : {p.Category}
   Stock : {p.StockQuantity} unités"));
        }

        private async Task<string> GetFallbackRecommendation(string query)
        {
            var categories = GetUniqueCategories(await GetAllProductsCachedAsync());

            return $@"Je n'ai pas trouvé de produits correspondant exactement à ""{query}"".

**Catégories disponibles** : {string.Join(", ", categories.Take(5))}

💡 **Suggestions** :
• Essayez des termes plus généraux
• Consultez nos catégories
• Demandez-moi des recommandations par catégorie

Comment puis-je vous aider autrement ?";
        }

        private async Task<List<ProductDto>> GetAllProductsCachedAsync()
        {
            return await _memoryCache.GetOrCreateAsync("all_products", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _productService.GetAllAsync();
            }) ?? new List<ProductDto>();
        }

        private List<string> GetUniqueCategories(List<ProductDto> products)
        {
            return products
                .Select(p => p.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();
        }

        private List<string> SimpleKeywordExtraction(string query)
        {
            var stopWords = new HashSet<string>
            {
                "le", "la", "les", "un", "une", "des", "du", "de", "et",
                "ou", "où", "à", "au", "aux", "dans", "sur", "avec",
                "pour", "par", "est", "sont", "que", "qui", "quoi"
            };

            return query.ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .Distinct()
                .Take(5)
                .ToList();
        }

        private string TruncateText(string? text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }
    }
}