using E_commerce.Models.DTOs;
using System.Text.Json;

namespace E_commerce.Services.Rag
{
    /// <summary>
    /// Service de vectorisation et recherche sémantique pour les produits
    /// Utilise des embeddings pour comprendre l'intention de l'utilisateur
    /// </summary>
    public class VectorProductIndexService
    {
        private readonly GeminiService _geminiService;
        private readonly ILogger<VectorProductIndexService> _logger;
        private readonly string _cachePath;
        
        // Index en mémoire: ProductId -> Embedding Vector
        private Dictionary<Guid, ProductVector> _productIndex;
        private readonly SemaphoreSlim _indexLock = new SemaphoreSlim(1, 1);
        private bool _isIndexReady = false;

        public VectorProductIndexService(
            GeminiService geminiService,
            ILogger<VectorProductIndexService> logger,
            IWebHostEnvironment env)
        {
            _geminiService = geminiService;
            _logger = logger;
            _cachePath = Path.Combine(env.ContentRootPath, "Cache", "product_embeddings.json");
            _productIndex = new Dictionary<Guid, ProductVector>();
            
            // Créer le dossier Cache s'il n'existe pas
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
        }

        public bool IsIndexReady => _isIndexReady;

        /// <summary>
        /// Construit l'index vectoriel pour tous les produits
        /// </summary>
        public async Task BuildIndexAsync(List<ProductDto> products, bool forceRebuild = false)
        {
            await _indexLock.WaitAsync();
            try
            {
                _logger.LogInformation("🚀 Construction de l'index vectoriel pour {Count} produits...", products.Count);

                // Charger depuis le cache si disponible
                if (!forceRebuild && await LoadFromCacheAsync())
                {
                    // Vérifier si tous les produits sont indexés
                    var cachedIds = _productIndex.Keys.ToHashSet();
                    var productIds = products.Select(p => p.Id).ToHashSet();
                    
                    if (cachedIds.IsSupersetOf(productIds))
                    {
                        _logger.LogInformation("✅ Index chargé depuis le cache ({Count} produits)", _productIndex.Count);
                        _isIndexReady = true;
                        return;
                    }
                    
                    _logger.LogInformation("⚠️ Cache incomplet, reconstruction nécessaire");
                }

                // Construire les embeddings
                var newIndex = new Dictionary<Guid, ProductVector>();
                int processed = 0;
                int success = 0;
                int failed = 0;

                foreach (var product in products)
                {
                    try
                    {
                        // Créer le texte à vectoriser (combinaison de plusieurs champs)
                        var searchableText = BuildSearchableText(product);
                        
                        // Générer l'embedding
                        var embedding = await _geminiService.GetEmbeddingAsync(searchableText);
                        
                        if (embedding != null && embedding.Length > 0)
                        {
                            newIndex[product.Id] = new ProductVector
                            {
                                ProductId = product.Id,
                                ProductName = product.Name,
                                Embedding = embedding,
                                SearchableText = searchableText,
                                CreatedAt = DateTime.UtcNow
                            };
                            success++;
                        }
                        else
                        {
                            _logger.LogWarning("❌ Échec embedding pour produit {ProductId}", product.Id);
                            failed++;
                        }

                        processed++;
                        
                        if (processed % 10 == 0)
                        {
                            _logger.LogInformation("Progression: {Processed}/{Total} produits", processed, products.Count);
                        }

                        // Respecter les limites de taux de l'API
                        await Task.Delay(150);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erreur lors de la vectorisation du produit {ProductId}", product.Id);
                        failed++;
                    }
                }

                _productIndex = newIndex;
                _isIndexReady = true;

                _logger.LogInformation("✅ Index vectoriel construit: {Success} succès, {Failed} échecs", success, failed);

                // Sauvegarder dans le cache
                await SaveToCacheAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur critique lors de la construction de l'index");
                _isIndexReady = false;
            }
            finally
            {
                _indexLock.Release();
            }
        }

        /// <summary>
        /// Recherche sémantique basée sur les embeddings
        /// </summary>
        public async Task<List<ScoredProduct>> SearchAsync(string query, int topK = 10)
        {
            if (!_isIndexReady)
            {
                _logger.LogWarning("Index vectoriel non prêt");
                return new List<ScoredProduct>();
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<ScoredProduct>();
            }

            try
            {
                // Générer l'embedding de la requête
                var queryEmbedding = await _geminiService.GetEmbeddingAsync(query);
                
                if (queryEmbedding == null || queryEmbedding.Length == 0)
                {
                    _logger.LogWarning("Impossible de générer l'embedding pour la requête");
                    return new List<ScoredProduct>();
                }

                // Calculer les similarités
                var results = new List<ScoredProduct>();

                foreach (var (productId, productVector) in _productIndex)
                {
                    var similarity = ComputeCosineSimilarity(queryEmbedding, productVector.Embedding);
                    
                    results.Add(new ScoredProduct
                    {
                        ProductId = productId,
                        ProductName = productVector.ProductName,
                        SemanticScore = similarity,
                        SearchableText = productVector.SearchableText
                    });
                }

                // Trier par score, filtrer par seuil minimum et retourner les top-K
                const float MIN_SIMILARITY_THRESHOLD = 0.3f; // Seuil minimum de pertinence
                
                var topResults = results
                    .Where(r => r.SemanticScore >= MIN_SIMILARITY_THRESHOLD) // ✅ Filtrer les non-pertinents
                    .OrderByDescending(r => r.SemanticScore)
                    .Take(topK)
                    .ToList();

                // Log pour debug
                if (topResults.Any())
                {
                    var bestMatch = topResults.First();
                    _logger.LogInformation("🔍 Recherche '{Query}': Meilleur match = {Product} (score: {Score:F3}), {Count} résultats",
                        query, bestMatch.ProductName, bestMatch.SemanticScore, topResults.Count);
                }
                else
                {
                    _logger.LogWarning("⚠️ Aucun produit avec similarité >= {Threshold} pour '{Query}'", MIN_SIMILARITY_THRESHOLD, query);
                }

                return topResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la recherche vectorielle");
                return new List<ScoredProduct>();
            }
        }

        /// <summary>
        /// Calcule la similarité cosinus entre deux vecteurs
        /// Résultat entre -1 et 1, où 1 = identique, 0 = orthogonal, -1 = opposé
        /// </summary>
        public static float ComputeCosineSimilarity(float[] vector1, float[] vector2)
        {
            if (vector1.Length != vector2.Length)
            {
                throw new ArgumentException("Les vecteurs doivent avoir la même dimension");
            }

            float dotProduct = 0f;
            float magnitude1 = 0f;
            float magnitude2 = 0f;

            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];
            }

            magnitude1 = MathF.Sqrt(magnitude1);
            magnitude2 = MathF.Sqrt(magnitude2);

            if (magnitude1 == 0f || magnitude2 == 0f)
            {
                return 0f;
            }

            return dotProduct / (magnitude1 * magnitude2);
        }

        /// <summary>
        /// Ajoute un nouveau produit à l'index
        /// </summary>
        public async Task AddProductAsync(ProductDto product)
        {
            await _indexLock.WaitAsync();
            try
            {
                var searchableText = BuildSearchableText(product);
                var embedding = await _geminiService.GetEmbeddingAsync(searchableText);

                if (embedding != null && embedding.Length > 0)
                {
                    _productIndex[product.Id] = new ProductVector
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Embedding = embedding,
                        SearchableText = searchableText,
                        CreatedAt = DateTime.UtcNow
                    };

                    await SaveToCacheAsync();
                    _logger.LogInformation("✅ Produit {ProductId} ajouté à l'index", product.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'ajout du produit {ProductId} à l'index", product.Id);
            }
            finally
            {
                _indexLock.Release();
            }
        }

        /// <summary>
        /// Supprime un produit de l'index
        /// </summary>
        public async Task RemoveProductAsync(Guid productId)
        {
            await _indexLock.WaitAsync();
            try
            {
                if (_productIndex.Remove(productId))
                {
                    await SaveToCacheAsync();
                    _logger.LogInformation("✅ Produit {ProductId} retiré de l'index", productId);
                }
            }
            finally
            {
                _indexLock.Release();
            }
        }

        // ==================== MÉTHODES PRIVÉES ====================

        private string BuildSearchableText(ProductDto product)
        {
            // Combiner plusieurs champs pour une meilleure représentation sémantique
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(product.Name))
                parts.Add(product.Name);

            if (!string.IsNullOrEmpty(product.Brand))
                parts.Add($"Marque: {product.Brand}");

            if (!string.IsNullOrEmpty(product.Category))
                parts.Add($"Catégorie: {product.Category}");

            if (!string.IsNullOrEmpty(product.Description))
            {
                // Tronquer la description pour économiser les tokens
                var desc = product.Description.Length > 200
                    ? product.Description.Substring(0, 200) + "..."
                    : product.Description;
                parts.Add(desc);
            }

            parts.Add($"Prix: {product.Price:C}");

            return string.Join(". ", parts);
        }

        private async Task<bool> LoadFromCacheAsync()
        {
            try
            {
                if (!File.Exists(_cachePath))
                {
                    _logger.LogInformation("Aucun cache d'embeddings trouvé");
                    return false;
                }

                var json = await File.ReadAllTextAsync(_cachePath);
                var cached = JsonSerializer.Deserialize<List<ProductVector>>(json);

                if (cached == null || cached.Count == 0)
                {
                    return false;
                }

                _productIndex = cached.ToDictionary(pv => pv.ProductId, pv => pv);
                
                _logger.LogInformation("📦 Cache chargé: {Count} embeddings", _productIndex.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erreur lors du chargement du cache");
                return false;
            }
        }

        private async Task SaveToCacheAsync()
        {
            try
            {
                var data = _productIndex.Values.ToList();
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_cachePath, json);
                _logger.LogDebug("💾 Cache sauvegardé: {Count} embeddings", data.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la sauvegarde du cache");
            }
        }

        public Dictionary<string, object> GetIndexStats()
        {
            return new Dictionary<string, object>
            {
                ["isReady"] = _isIndexReady,
                ["totalProducts"] = _productIndex.Count,
                ["cacheExists"] = File.Exists(_cachePath),
                ["embeddingDimension"] = _productIndex.Values.FirstOrDefault()?.Embedding.Length ?? 0
            };
        }
    }

    // ==================== CLASSES AUXILIAIRES ====================

    public class ProductVector
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public string SearchableText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ScoredProduct
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public float SemanticScore { get; set; }
        public string SearchableText { get; set; } = string.Empty;
    }
}
