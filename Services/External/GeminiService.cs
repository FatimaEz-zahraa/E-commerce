using System.Net.Http.Headers;
using System.Text.Json;

namespace E_commerce.Services.External
{
    public interface IGeminiService
    {
        Task<string> GenerateProductDescriptionAsync(string category, string productName);
    }

    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GeminiService> _logger;

        public GeminiService(IConfiguration configuration, ILogger<GeminiService> logger)
        {
            _httpClient = new HttpClient();
            _apiKey = configuration["GeminiAPI:ApiKey"] ?? string.Empty;
            _logger = logger;

            // Configure les headers par défaut
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<string> GenerateProductDescriptionAsync(string category, string productName)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    _logger.LogWarning("Clé API Gemini non configurée");
                    return GetDefaultDescription(productName, category);
                }

                // Utilisez gemini-2.0-flash comme dans votre test curl
                var apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

                var prompt = $"Crée une description marketing pour: {productName} (catégorie: {category}). " +
                            "Description en français, persuasive, 120 caractères maximum. " +
                            "Utilise un ton commercial.";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        topP = 0.8,
                        topK = 40,
                        maxOutputTokens = 150
                    }
                };

                // Deux méthodes possibles :

                // Méthode 1 : Via le header X-goog-api-key (comme votre curl)
                _httpClient.DefaultRequestHeaders.Remove("X-goog-api-key");
                _httpClient.DefaultRequestHeaders.Add("X-goog-api-key", _apiKey);

                var response = await _httpClient.PostAsJsonAsync(apiUrl, requestBody);

                // Méthode 2 : Via query parameter (alternative)
                // var apiUrlWithKey = $"{apiUrl}?key={_apiKey}";
                // var response = await _httpClient.PostAsJsonAsync(apiUrlWithKey, requestBody);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug($"Réponse Gemini: {json}");

                    var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(json);

                    var description = geminiResponse?.Candidates?.FirstOrDefault()?
                        .Content?.Parts?.FirstOrDefault()?.Text?.Trim();

                    if (!string.IsNullOrEmpty(description))
                    {
                        _logger.LogInformation($"✅ Description générée pour: {productName}");
                        return description;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"❌ Erreur Gemini: {response.StatusCode} - {errorContent}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Erreur HTTP lors de l'appel à Gemini");
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Erreur de parsing JSON de la réponse Gemini");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue avec Gemini");
            }

            // Fallback à une description par défaut
            return GetDefaultDescription(productName, category);
        }

        private string GetDefaultDescription(string productName, string category)
        {
            var defaultDescriptions = new[]
            {
                $"{productName} - Un produit exceptionnel dans la catégorie {category}.",
                $"{productName}: qualité premium pour un usage quotidien.",
                $"Découvrez {productName}, l'innovation dans {category}.",
                $"{productName}: performance et fiabilité garanties.",
                $"{productName} - Le must-have de {category}."
            };

            return defaultDescriptions[new Random().Next(defaultDescriptions.Length)];
        }
    }

    // Classes pour la désérialisation
    public class GeminiResponse
    {
        public List<Candidate> Candidates { get; set; } = new();
    }

    public class Candidate
    {
        public Content Content { get; set; } = new();
    }

    public class Content
    {
        public List<Part> Parts { get; set; } = new();
    }

    public class Part
    {
        public string Text { get; set; } = string.Empty;
    }
}