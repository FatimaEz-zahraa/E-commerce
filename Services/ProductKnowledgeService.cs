// Services/ProductKnowledgeService.cs
using System.Text;
using System.Text.Json;
using E_commerce.Data;
using E_commerce.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace E_commerce.Services
{
    public class ProductKnowledgeService
    {
        private readonly AppDbContext _context;
        private readonly IDistributedCache _cache;
        private readonly ILogger<ProductKnowledgeService> _logger;

        // Cache pour les catégories
        private List<CategoryMetadata>? _categoryCache = null;
        private DateTime _categoryCacheTime = DateTime.MinValue;
        private readonly TimeSpan _categoryCacheDuration = TimeSpan.FromMinutes(5);

        public ProductKnowledgeService(
            AppDbContext context,
            IDistributedCache cache,
            ILogger<ProductKnowledgeService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }


        /// <summary>
        /// Initialise le cache des produits (appelé au démarrage)
        /// </summary>
        public async Task InitializeCacheAsync()
        {
            try
            {
                _logger.LogInformation("🚀 Initialisation du cache ProductKnowledge...");

                // 1. Précharger les métadonnées des catégories
                await GetCategoryMetadataAsync();

                // 2. Initialiser quelques contextes de base
                await InitializeBaseContextsAsync();

                // 3. Vérifier la connexion à la base
                var productCount = await _context.Products.CountAsync(p => p.IsActive);
                var categoryCount = await _context.Products
                    .Where(p => p.IsActive)
                    .Select(p => p.Category)
                    .Distinct()
                    .CountAsync();

                _logger.LogInformation($"✅ Cache initialisé: {productCount} produits, {categoryCount} catégories");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erreur d'initialisation du cache");
                throw;
            }
        }

        /// <summary>
        /// Initialise les contextes de base pour les requêtes courantes
        /// </summary>
        private async Task InitializeBaseContextsAsync()
        {
            var commonQueries = new[]
            {
        "smartphone",
        "ordinateur",
        "promotion",
        "meilleur produit",
        "livraison",
        "prix bas"
    };

            foreach (var query in commonQueries)
            {
                try
                {
                    var cacheKey = $"rag_context_{query.GetHashCode()}";
                    var context = await BuildGenericContextForQueryAsync(query);

                    await _cache.SetStringAsync(
                        cacheKey,
                        context,
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                        });

                    _logger.LogDebug($"Cache initialisé pour: {query}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Impossible de cacher la requête: {query}");
                }
            }
        }

        /// <summary>
        /// Construit un contexte générique pour une requête
        /// </summary>
        private async Task<string> BuildGenericContextForQueryAsync(string query)
        {
            var analysis = await AnalyzeQueryAsync(query);
            var products = await FindRelevantProductsGenericAsync(analysis);

            var context = new StringBuilder();
            context.AppendLine("### CONTEXTE PRÉCHARGÉ ###");
            context.AppendLine($"Pour la requête: \"{query}\"");

            if (products.Any())
            {
                context.AppendLine($"Produits pertinents: {products.Count}");
                foreach (var product in products.Take(3))
                {
                    context.AppendLine($"- {product.Name} ({product.Brand}) - {product.Price:C}");
                }
            }
            else
            {
                context.AppendLine("Aucun produit spécifique trouvé");
            }

            return context.ToString();
        }

        /// <summary>
        /// Récupère le contexte pertinent pour une question
        /// </summary>
        public async Task<string> GetRelevantContextAsync(string question, ProductDto? currentProduct = null)
        {
            try
            {
                var cacheKey = $"rag_context_{question.GetHashCode()}";
                var cachedContext = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedContext))
                    return cachedContext;

                // 1. Analyser la requête
                var analysis = await AnalyzeQueryAsync(question);

                // 2. Rechercher les produits pertinents
                var relevantProducts = await FindRelevantProductsGenericAsync(analysis, currentProduct?.Id);

                // 3. Construire le contexte
                var context = await BuildGenericContextAsync(relevantProducts, currentProduct, analysis, question);

                // Mettre en cache
                await _cache.SetStringAsync(
                    cacheKey,
                    context,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                    });

                return context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération du contexte RAG");
                return await GetFallbackContextAsync();
            }
        }

        /// <summary>
        /// Analyse d'une requête utilisateur
        /// </summary>
        private async Task<QueryAnalysis> AnalyzeQueryAsync(string query)
        {
            var lowerQuery = query.ToLowerInvariant();
            var categories = await GetCategoryMetadataAsync();

            var analysis = new QueryAnalysis
            {
                OriginalQuery = query,
                NormalizedQuery = lowerQuery,
                Keywords = ExtractSmartKeywords(lowerQuery),
                DetectedCategories = new List<DetectedCategory>(),
                DetectedBrands = new List<string>(),
                Intent = DetectIntent(lowerQuery)
            };

            // Détection des catégories
            foreach (var category in categories)
            {
                var relevance = CalculateCategoryRelevance(category, lowerQuery);
                if (relevance.Score > 0.3)
                {
                    analysis.DetectedCategories.Add(relevance);
                }
            }

            // Détection des marques
            analysis.DetectedBrands = DetectBrands(lowerQuery, categories);

            return analysis;
        }

        /// <summary>
        /// Extraction des mots-clés
        /// </summary>
        private List<string> ExtractSmartKeywords(string query)
        {
            var stopWords = new HashSet<string>
            {
                "le","la","les","un","une","des","du","de","et","ou","où","à","au","aux","dans","sur","avec",
                "pour","par","est","sont","ces","cette","ce","je","tu","il","elle","nous","vous","ils","elles"
            };

            return query
                .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLowerInvariant().Trim())
                .Where(t => t.Length > 2 && !stopWords.Contains(t))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Obtenir les métadonnées des catégories
        /// </summary>
        private async Task<List<CategoryMetadata>> GetCategoryMetadataAsync()
        {
            if (_categoryCache != null && DateTime.Now - _categoryCacheTime < _categoryCacheDuration)
            {
                return _categoryCache;
            }

            var categories = await _context.Products
                .Where(p => p.IsActive)
                .GroupBy(p => p.Category)
                .Select(g => new CategoryMetadata
                {
                    Name = g.Key,
                    ProductCount = g.Count(),
                    TopBrands = g
                        .GroupBy(p => p.Brand)
                        .OrderByDescending(bg => bg.Count())
                        .Take(5)
                        .Select(bg => bg.Key)
                        .ToList(),
                    AveragePrice = g.Average(p => p.Price),
                    AverageRating = (double)g.Average(p => p.Rating)
                })
                .Where(c => !string.IsNullOrEmpty(c.Name))
                .OrderByDescending(c => c.ProductCount)
                .ToListAsync();

            // Générer les synonymes
            foreach (var category in categories)
            {
                category.Synonyms = GenerateSynonyms(category.Name);
            }

            _categoryCache = categories;
            _categoryCacheTime = DateTime.Now;

            return categories;
        }

        /// <summary>
        /// Générer des synonymes pour une catégorie
        /// </summary>
        private List<string> GenerateSynonyms(string categoryName)
        {
            var synonyms = new List<string> { categoryName.ToLowerInvariant() };
            var lowerName = categoryName.ToLowerInvariant();

            var baseSynonyms = new Dictionary<string, List<string>>
            {
                { "électronique", new List<string> { "électronique", "tech", "gadget", "appareil" } },
                { "informatique", new List<string> { "ordinateur", "pc", "laptop", "computer" } },
                { "téléphonie", new List<string> { "téléphone", "smartphone", "mobile", "phone" } },
                { "maison", new List<string> { "décoration", "ameublement", "home", "cuisine" } },
                { "mode", new List<string> { "vêtement", "fashion", "habillement", "style" } }
            };

            if (baseSynonyms.TryGetValue(lowerName, out var baseSyns))
            {
                synonyms.AddRange(baseSyns);
            }

            return synonyms.Distinct().ToList();
        }

        /// <summary>
        /// Calculer la pertinence d'une catégorie
        /// </summary>
        private DetectedCategory CalculateCategoryRelevance(CategoryMetadata category, string query)
        {
            var score = 0.0;
            var matchedKeywords = new List<string>();

            foreach (var synonym in category.Synonyms)
            {
                if (query.Contains(synonym, StringComparison.OrdinalIgnoreCase))
                {
                    score += 1.0;
                    matchedKeywords.Add(synonym);
                }
            }

            return new DetectedCategory
            {
                Category = category.Name,
                Score = score,
                MatchedKeywords = matchedKeywords,
                ProductCount = category.ProductCount
            };
        }

        /// <summary>
        /// Recherche de produits pertinents
        /// </summary>
        private async Task<List<Product>> FindRelevantProductsGenericAsync(
            QueryAnalysis analysis,
            Guid? excludeProductId = null,
            int limit = 10)
        {
            var query = _context.Products
                .Include(p => p.Reviews)
                .Where(p => p.IsActive);

            if (excludeProductId.HasValue)
                query = query.Where(p => p.Id != excludeProductId.Value);

            var results = new List<(Product Product, double Score)>();

            // Recherche par catégories
            foreach (var detectedCategory in analysis.DetectedCategories.OrderByDescending(c => c.Score))
            {
                var categoryProducts = await query
                    .Where(p => p.Category == detectedCategory.Category)
                    .ToListAsync();

                foreach (var product in categoryProducts)
                {
                    AddOrUpdateScore(results, product, detectedCategory.Score * 2.0);
                }
            }

            // Recherche par marques
            foreach (var brand in analysis.DetectedBrands)
            {
                var brandProducts = await query
                    .Where(p => p.Brand.ToLower().Contains(brand.ToLower()))
                    .ToListAsync();

                foreach (var product in brandProducts)
                {
                    AddOrUpdateScore(results, product, 1.5);
                }
            }

            // Recherche par mots-clés
            foreach (var keyword in analysis.Keywords)
            {
                var keywordProducts = await query
                    .Where(p => p.Name.ToLower().Contains(keyword.ToLower()) ||
                               p.Description.ToLower().Contains(keyword.ToLower()))
                    .ToListAsync();

                foreach (var product in keywordProducts)
                {
                    AddOrUpdateScore(results, product, 1.0);
                }
            }

            // Trier et limiter
            return results
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => r.Product.Rating)
                .Take(limit)
                .Select(r => r.Product)
                .ToList();
        }

        private void AddOrUpdateScore(List<(Product Product, double Score)> results, Product product, double score)
        {
            var existing = results.FirstOrDefault(r => r.Product.Id == product.Id);
            if (existing.Product != null)
            {
                var index = results.IndexOf(existing);
                results[index] = (product, existing.Score + score);
            }
            else
            {
                results.Add((product, score));
            }
        }

        /// <summary>
        /// Construire le contexte
        /// </summary>
        private async Task<string> BuildGenericContextAsync(
            List<Product> products,
            ProductDto? currentProduct,
            QueryAnalysis analysis,
            string originalQuestion)
        {
            var sb = new StringBuilder();

            // En-tête
            sb.AppendLine("### CONTEXTE E-COMMERCE ###");
            sb.AppendLine($"Question : \"{originalQuestion}\"");
            sb.AppendLine($"Intent détecté : {analysis.Intent}");

            if (analysis.DetectedCategories.Any())
            {
                sb.AppendLine($"Catégories pertinentes : {string.Join(", ", analysis.DetectedCategories.Select(c => c.Category).Take(3))}");
            }
            sb.AppendLine();

            // Produits trouvés
            if (products.Any())
            {
                sb.AppendLine($"📊 **{products.Count} PRODUITS PERTINENTS**");

                // Grouper par catégorie
                var productsByCategory = products
                    .GroupBy(p => p.Category)
                    .OrderByDescending(g => g.Count());

                foreach (var categoryGroup in productsByCategory)
                {
                    sb.AppendLine($"\n📁 **{categoryGroup.Key}**");

                    foreach (var product in categoryGroup.OrderByDescending(p => p.Rating).Take(3))
                    {
                        decimal avgRating = product.Reviews?.Any() == true
                                            ? product.Reviews.Average(r => (decimal)r.Rating)
                                            : product.Rating;




                        sb.AppendLine($"  • {product.Name} ({product.Brand})");
                        sb.AppendLine($"    Prix: {product.Price:C} | Note: {avgRating:F1}/5");
                        sb.AppendLine($"    {TruncateText(product.Description, 80)}");
                    }
                }
            }
            else
            {
                sb.AppendLine("🔍 **AUCUN PRODUIT EXACT TROUVÉ**");
                sb.AppendLine("Voici nos principales catégories :");

                var topCategories = await _context.Products
                    .Where(p => p.IsActive)
                    .GroupBy(p => p.Category)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => g.Key)
                    .ToListAsync();

                foreach (var category in topCategories)
                {
                    sb.AppendLine($"  • {category}");
                }
            }

            // Statistiques
            sb.AppendLine("\n📈 **STATISTIQUES**");
            var totalProducts = await _context.Products.CountAsync(p => p.IsActive);
            var totalCategories = await _context.Products
                .Where(p => p.IsActive)
                .Select(p => p.Category)
                .Distinct()
                .CountAsync();

            sb.AppendLine($"• Total produits : {totalProducts}");
            sb.AppendLine($"• Catégories : {totalCategories}");

            return sb.ToString();
        }

        // Méthodes utilitaires
        private string DetectIntent(string query)
        {
            if (query.Contains("acheter") || query.Contains("achat") || query.Contains("commander"))
                return "achat";
            if (query.Contains("comparer") || query.Contains("vs") || query.Contains("différence"))
                return "comparaison";
            if (query.Contains("recommand") || query.Contains("meilleur") || query.Contains("top"))
                return "recommandation";
            if (query.Contains("prix") || query.Contains("coût") || query.Contains("combien"))
                return "prix";
            return "information";
        }

        private List<string> DetectBrands(string query, List<CategoryMetadata> categories)
        {
            var brands = new List<string>();
            var allBrands = categories.SelectMany(c => c.TopBrands).Distinct().ToList();

            foreach (var brand in allBrands)
            {
                if (query.Contains(brand, StringComparison.OrdinalIgnoreCase))
                {
                    brands.Add(brand);
                }
            }

            return brands.Distinct().ToList();
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }

        private async Task<string> GetFallbackContextAsync()
        {
            var categories = await _context.Products
                .Where(p => p.IsActive)
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .Take(3)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("📦 **CONTEXTE DE BASE**");
            sb.AppendLine("Catégories disponibles :");
            foreach (var category in categories)
            {
                sb.AppendLine($"  • {category}");
            }

            var popular = await _context.Products
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.Rating)
                .Take(2)
                .Select(p => new { p.Name, p.Price })
                .ToListAsync();

            if (popular.Any())
            {
                sb.AppendLine("\nProduits populaires :");
                foreach (var p in popular)
                {
                    sb.AppendLine($"  • {p.Name} - {p.Price:C}");
                }
            }

            return sb.ToString();
        }

        // Classes internes
        private class QueryAnalysis
        {
            public string OriginalQuery { get; set; } = string.Empty;
            public string NormalizedQuery { get; set; } = string.Empty;
            public List<string> Keywords { get; set; } = new();
            public List<DetectedCategory> DetectedCategories { get; set; } = new();
            public List<string> DetectedBrands { get; set; } = new();
            public string Intent { get; set; } = "information";
        }

        private class DetectedCategory
        {
            public string Category { get; set; } = string.Empty;
            public double Score { get; set; }
            public List<string> MatchedKeywords { get; set; } = new();
            public int ProductCount { get; set; }
        }

        private class CategoryMetadata
        {
            public string Name { get; set; } = string.Empty;
            public int ProductCount { get; set; }
            public List<string> TopBrands { get; set; } = new();
            public List<string> Synonyms { get; set; } = new();
            public decimal AveragePrice { get; set; }
            public double AverageRating { get; set; }
        }
    }
}