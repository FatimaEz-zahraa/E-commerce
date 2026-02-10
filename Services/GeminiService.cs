// Services/GeminiService.cs
using E_commerce.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace E_commerce.Services
{
    public class GeminiService 
    {
        private readonly GeminiOptions _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GeminiService> _logger;
        private readonly HttpClient _httpClient;

        public GeminiService(
            IOptions<GeminiOptions> config,
            IHttpClientFactory httpClientFactory,
            ILogger<GeminiService> logger)
        {
            _config = config.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            // Créer un HttpClient spécifique pour Gemini
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ECommerce-App/1.0");
        }

        public async Task<string> AskAsync(string systemPrompt, string userPrompt, string? context = null)
        {
            try
            {
                // VÉRIFIEZ si la clé API Gemini est valide
                if (string.IsNullOrEmpty(_config.ApiKey))
                {
                    _logger.LogWarning("Clé API Gemini manquante");
                    return "L'assistant IA nécessite une clé API Gemini valide.";
                }

                if (!_config.Enabled)
                {
                    return "Le service Gemini est temporairement désactivé.";
                }

                // Liste des modèles DISPONIBLES d'après votre réponse
                // Ces modèles supportent "generateContent" et sont gratuits
                var modelsToTry = new[]
                {
                    "gemini-2.0-flash-001",           // Stable version, devrait fonctionner
                    "gemini-2.0-flash-lite-001",      // Lite version stable
                    "gemini-2.0-flash",               // Version standard
                    "gemini-2.0-flash-lite",          // Version lite
                    "gemini-flash-latest",           // Dernière version flash
                    "gemini-flash-lite-latest",      // Dernière version flash lite
                    "gemini-pro-latest",             // Dernière version pro
                    "gemini-2.5-flash",              // Version 2.5 flash
                    "gemini-2.5-flash-lite",         // Version 2.5 flash lite
                    "gemma-3-1b-it",                 // Petit modèle Gemma 3
                    "gemma-3-4b-it"                  // Modèle Gemma 3 moyen
                };

                string lastError = "Aucun modèle testé";

                foreach (var model in modelsToTry)
                {
                    try
                    {
                        _logger.LogInformation("Essai avec le modèle: {Model}", model);
                        var result = await TryModelAsync(model, systemPrompt, userPrompt, context);

                        // Vérifier si c'est une vraie réponse (pas une erreur)
                        if (!IsErrorMessage(result))
                        {
                            _logger.LogInformation("✅ Succès avec le modèle: {Model}", model);
                            return result;
                        }

                        lastError = result;
                        _logger.LogWarning("Modèle {Model} a échoué: {Error}", model, result);
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                        _logger.LogWarning("Exception avec modèle {Model}: {Error}", model, ex.Message);
                    }
                }

                return $"⚠️ Aucun modèle Gemini disponible. Dernière erreur: {lastError}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GeminiService");
                return "🤖 Désolé, je rencontre des difficultés techniques. Veuillez réessayer.";
            }
        }

        private bool IsErrorMessage(string response)
        {
            return response.StartsWith("⚠️") ||
                   response.StartsWith("❌") ||
                   response.StartsWith("🌐") ||
                   response.StartsWith("🤖 Erreur") ||
                   response.Contains("non trouvé") ||
                   response.Contains("Erreur") ||
                   response.Contains("error", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> TryModelAsync(string model, string systemPrompt, string userPrompt, string? context)
        {
            try
            {
                // URL correcte pour l'API Gemini - utiliser le nom COMPLET du modèle
                var fullModelName = $"models/{model}";
                var url = $"/v1beta/{fullModelName}:generateContent?key={_config.ApiKey}";

                _logger.LogDebug("Envoi requête à: {Url}", url);

                // Construction du prompt simplifié
                var fullPrompt = BuildGeminiPrompt(systemPrompt, userPrompt, context);

                var requestData = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = fullPrompt
                                }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = _config.Temperature,
                        maxOutputTokens = _config.MaxTokens,
                        topP = 0.8,
                        topK = 40
                    },
                    safetySettings = new[]
                    {
                        new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                        new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                        new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                        new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" }
                    }
                };

                var jsonRequest = JsonSerializer.Serialize(requestData);
                _logger.LogDebug("Requête JSON: {Json}", jsonRequest);

                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                // GESTION DES ERREURS HTTP
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();

                    // Essayer de parser l'erreur JSON
                    try
                    {
                        var errorJson = JsonDocument.Parse(errorContent);
                        if (errorJson.RootElement.TryGetProperty("error", out var errorObj))
                        {
                            if (errorObj.TryGetProperty("message", out var message))
                            {
                                var errorMsg = message.GetString();
                                _logger.LogError("Erreur Gemini: {Message}", errorMsg);

                                if (errorMsg?.Contains("API key") == true ||
                                    errorMsg?.Contains("permission") == true)
                                {
                                    return $"⚠️ Problème d'authentification: {errorMsg}";
                                }

                                return $"⚠️ {errorMsg}";
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Si pas JSON, afficher le texte brut
                        _logger.LogError("Erreur brute: {Error}", errorContent);
                    }

                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return $"⚠️ Modèle '{model}' non trouvé.";
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        return "⚠️ Clé API invalide ou expirée.";
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        return "⚠️ Trop de requêtes. Veuillez réessayer plus tard.";
                    }

                    return $"❌ Erreur HTTP {(int)response.StatusCode}";
                }

                var result = await ParseGeminiResponseAsync(response);
                return result;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Erreur réseau");
                return "🌐 Erreur de connexion au serveur Gemini.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur avec le modèle {Model}", model);
                return $"⚠️ Exception: {ex.Message}";
            }
        }

        private string BuildGeminiPrompt(string systemPrompt, string userPrompt, string? context)
        {
            var prompt = new StringBuilder();

            // Format optimisé pour Gemini
            prompt.AppendLine($"Instruction système: {systemPrompt}");
            prompt.AppendLine();

            if (!string.IsNullOrWhiteSpace(context))
            {
                prompt.AppendLine($"Contexte supplémentaire: {context}");
                prompt.AppendLine();
            }

            prompt.AppendLine($"Question utilisateur: {userPrompt}");
            prompt.AppendLine();
            prompt.AppendLine("Instructions: Réponds en français, sois concis et utile.");

            return prompt.ToString();
        }

        private async Task<string> ParseGeminiResponseAsync(HttpResponseMessage response)
        {
            try
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Réponse Gemini brute: {Response}", jsonResponse);

                using var jsonDoc = JsonDocument.Parse(jsonResponse);

                // Format standard Gemini
                if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];

                    // Vérifier si bloqué pour sécurité
                    if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
                    {
                        var reason = finishReason.GetString();
                        if (reason == "SAFETY" || reason == "RECITATION")
                        {
                            return "⚠️ La réponse a été filtrée pour des raisons de sécurité.";
                        }
                    }

                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        var text = parts[0].GetProperty("text").GetString();
                        return text?.Trim() ?? "🤖 Réponse vide.";
                    }
                }

                // Autres formats possibles
                if (jsonDoc.RootElement.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString()?.Trim() ?? "🤖 Réponse vide.";
                }

                _logger.LogWarning("Format de réponse inattendu");
                return "🤖 Format de réponse inattendu de Gemini.";
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Erreur d'analyse JSON");
                return "🤖 Erreur de traitement de la réponse JSON.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur d'analyse de la réponse");
                return "🤖 Erreur de traitement de la réponse.";
            }
        }

        /// <summary>
        /// Génère une description de produit automatiquement
        /// </summary>
        public async Task<string> GenerateProductDescriptionAsync(ProductDto product)
        {
            if (product == null) return "Produit non spécifié.";

            var systemPrompt = """
                Tu es un expert en marketing e-commerce spécialisé dans la rédaction de descriptions produits.
                Rédige une description attractive, persuasive et professionnelle en français.
                Inclus les avantages pour le client et un appel à l'action.
                """;

            var userPrompt = $"""
                Crée une description marketing pour ce produit:
                
                NOM: {product.Name}
                CATÉGORIE: {product.Category}
                MARQUE: {product.Brand}
                PRIX: {product.Price:C}
                
                DESCRIPTION EXISTANTE (à améliorer):
                {product.Description}
                
                Rédige 2-3 paragraphes maximum.
                """;

            return await AskAsync(systemPrompt, userPrompt);
        }

        /// <summary>
        /// Extrait les mots-clés de recherche d'une requête utilisateur
        /// </summary>
        public async Task<List<string>> ExtractSearchKeywordsAsync(string userQuery)
        {
            if (string.IsNullOrWhiteSpace(userQuery))
                return new List<string>();

            var systemPrompt = """
                Tu es un assistant expert en SEO e-commerce.
                Extrait les mots-clés de recherche les plus importants.
                Retourne UNIQUEMENT les mots-clés séparés par des virgules.
                Exclus les articles et mots non pertinents.
                Exemple: "ordinateur portable gaming" → "ordinateur, portable, gaming"
                """;

            var response = await AskAsync(systemPrompt,
                $"Extrait les mots-clés de recherche de: '{userQuery}'");

            // Nettoyer et parser la réponse
            var keywords = response.Split(',')
                .Select(k => k.Trim()
                    .ToLower()
                    .Replace(".", "")
                    .Replace(";", "")
                    .Replace("!", "")
                    .Replace("?", ""))
                .Where(k => !string.IsNullOrEmpty(k))
                .Where(k => k.Length > 2)
                .Distinct()
                .Take(7)
                .ToList();

            // Fallback si pas de résultats
            if (!keywords.Any())
            {
                keywords = userQuery.ToLower()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(word => word.Length > 2)
                    .Take(5)
                    .ToList();
            }

            return keywords;
        }

        /// <summary>
        /// Teste la connexion à Gemini avec un modèle spécifique
        /// </summary>
        public async Task<TestResult> TestConnectionAsync(string? model = null)
        {
            try
            {
                var testModel = model ?? "gemini-2.0-flash-001";
                var testUrl = $"/v1beta/models/{testModel}:generateContent?key={_config.ApiKey}";

                var requestData = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = "Réponds par 'TEST OK'." }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        maxOutputTokens = 10
                    }
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestData),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(testUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    return new TestResult
                    {
                        Success = true,
                        Model = testModel,
                        Message = "Connexion réussie"
                    };
                }
                else
                {
                    return new TestResult
                    {
                        Success = false,
                        Model = testModel,
                        Message = $"Erreur HTTP: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new TestResult
                {
                    Success = false,
                    Model = model ?? "inconnu",
                    Message = $"Exception: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Génère un vecteur d'embedding pour le texte donné
        /// </summary>
        public async Task<float[]?> GetEmbeddingAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("Texte vide fourni pour l'embedding");
                    return null;
                }

                if (string.IsNullOrEmpty(_config.ApiKey))
                {
                    _logger.LogWarning("Clé API Gemini manquante pour embedding");
                    return null;
                }

                // Modèle d'embedding de Gemini
                var embeddingModel = "text-embedding-004";
                var url = $"/v1beta/models/{embeddingModel}:embedContent?key={_config.ApiKey}";

                var requestData = new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new { text = text }
                        }
                    }
                };

                var jsonRequest = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erreur embedding API: {StatusCode} - {Error}", 
                        response.StatusCode, errorContent);
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(jsonResponse);

                // Parse la réponse: {"embedding": {"values": [0.1, 0.2, ...]}}
                if (jsonDoc.RootElement.TryGetProperty("embedding", out var embeddingObj) &&
                    embeddingObj.TryGetProperty("values", out var valuesArray))
                {
                    var values = new List<float>();
                    foreach (var value in valuesArray.EnumerateArray())
                    {
                        values.Add((float)value.GetDouble());
                    }

                    _logger.LogDebug("Embedding généré: {Dimension} dimensions", values.Count);
                    return values.ToArray();
                }

                _logger.LogWarning("Format de réponse embedding inattendu");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération d'embedding");
                return null;
            }
        }

        /// <summary>
        /// Génère des embeddings pour plusieurs textes en batch (optimisé)
        /// </summary>
        public async Task<Dictionary<string, float[]>> GetEmbeddingsBatchAsync(List<string> texts, int batchSize = 10)
        {
            var results = new Dictionary<string, float[]>();
            
            try
            {
                // Traiter par lots pour éviter de surcharger l'API
                for (int i = 0; i < texts.Count; i += batchSize)
                {
                    var batch = texts.Skip(i).Take(batchSize).ToList();
                    
                    foreach (var text in batch)
                    {
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        
                        var embedding = await GetEmbeddingAsync(text);
                        if (embedding != null)
                        {
                            results[text] = embedding;
                        }
                        
                        // Petite pause pour respecter les limites de taux
                        await Task.Delay(100);
                    }
                    
                    _logger.LogInformation("Batch {Current}/{Total} traité", 
                        Math.Min(i + batchSize, texts.Count), texts.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans le traitement batch des embeddings");
            }

            return results;
        }
    }

    public class TestResult
    {
        public bool Success { get; set; }
        public string Model { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

}