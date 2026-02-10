using System.Text;
using System.Text.Json;
using E_commerce.Data;
using E_commerce.Models.DTOs;
using E_commerce.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace E_commerce.Services.Implementations
{
    public interface IEnhancedRagService
    {
        Task<ChatResponse> ProcessChatAsync(ChatRequest request);
        Task<List<ProductDto>> GetRecommendedProductsAsync(string query, int limit = 6);
        Task<ConversationHistory> GetConversationHistoryAsync(string sessionId);
        Task ClearConversationHistoryAsync(string sessionId);
        Task<AssistantAnalytics> GetAnalyticsAsync();
    }

    public class EnhancedRagService : IEnhancedRagService
    {
        private readonly AppDbContext _context;
        private readonly IDistributedCache _cache;
        private readonly GeminiService _geminiService;
        private readonly ILogger<EnhancedRagService> _logger;
        private readonly IProductService _productService;

        private const string CONVERSATION_PREFIX = "conv_";
        private const int MAX_CONVERSATION_LENGTH = 10;
        private readonly TimeSpan CONVERSATION_TTL = TimeSpan.FromHours(2);

        public EnhancedRagService(
            AppDbContext context,
            IDistributedCache cache,
            GeminiService geminiService,
            ILogger<EnhancedRagService> logger,
            IProductService productService)
        {
            _context = context;
            _cache = cache;
            _geminiService = geminiService;
            _logger = logger;
            _productService = productService;
        }

        public async Task<ChatResponse> ProcessChatAsync(ChatRequest request)
        {
            try
            {
                var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

                // 1. Récupérer l'historique de conversation
                var conversation = await GetOrCreateConversationAsync(sessionId);

                // 2. Analyser l'intention
                var intent = await AnalyzeIntentAsync(request.Message);

                // 3. Récupérer le contexte des produits
                var (products, searchQuery) = await GetRelevantProductsWithFiltersAsync(request.Message, intent);

                // 4. Construire le contexte enrichi
                var context = await BuildEnrichedContextAsync(products, intent, searchQuery);

                // 5. Construire le prompt avec l'historique
                var systemPrompt = BuildSystemPrompt(intent);
                var userPrompt = BuildUserPrompt(request.Message, context, conversation);

                // 6. Appeler Gemini
                var aiResponse = await _geminiService.AskAsync(systemPrompt, userPrompt);

                // 7. Mettre à jour l'historique
                conversation.Messages.Add(new ChatMessage
                {
                    Role = "user",
                    Content = request.Message,
                    Timestamp = DateTime.UtcNow
                });

                conversation.Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = aiResponse,
                    Products = products,
                    Timestamp = DateTime.UtcNow
                });

                // Garder seulement les derniers messages
                if (conversation.Messages.Count > MAX_CONVERSATION_LENGTH * 2)
                {
                    conversation.Messages = conversation.Messages
                        .TakeLast(MAX_CONVERSATION_LENGTH * 2)
                        .ToList();
                }

                // 8. Sauvegarder la conversation
                await SaveConversationAsync(sessionId, conversation);

                // 9. Formater la réponse
                return new ChatResponse
                {
                    Success = true,
                    Message = aiResponse,
                    RecommendedProducts = products,
                    SearchQuery = searchQuery,
                    SessionId = sessionId,
                    Intent = intent,
                    Suggestions = await GenerateFollowUpSuggestionsAsync(intent, products),
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat request");
                return new ChatResponse
                {
                    Success = false,
                    Message = "Je rencontre des difficultés techniques. Veuillez réessayer.",
                    SessionId = request.SessionId,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        public async Task<List<ProductDto>> GetRecommendedProductsAsync(string query, int limit = 6)
        {
            var intent = await AnalyzeIntentAsync(query);
            var (products, _) = await GetRelevantProductsWithFiltersAsync(query, intent);
            return products.Take(limit).ToList();
        }

        public async Task<ConversationHistory> GetConversationHistoryAsync(string sessionId)
        {
            return await GetOrCreateConversationAsync(sessionId);
        }

        public async Task ClearConversationHistoryAsync(string sessionId)
        {
            var cacheKey = $"{CONVERSATION_PREFIX}{sessionId}";
            await _cache.RemoveAsync(cacheKey);
        }

        public async Task<AssistantAnalytics> GetAnalyticsAsync()
        {
            var totalProducts = await _context.Products.CountAsync(p => p.IsActive);
            var totalCategories = await _context.Products
                .Where(p => p.IsActive)
                .Select(p => p.Category)
                .Distinct()
                .CountAsync();

            var popularCategories = await _context.Products
                .Where(p => p.IsActive)
                .GroupBy(p => p.Category)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new CategoryStats
                {
                    Name = g.Key,
                    ProductCount = g.Count(),
                    AvgPrice = g.Average(p => p.Price),
                    AvgRating = (double)g.Average(p => p.Rating)
                })
                .ToListAsync();

            return new AssistantAnalytics
            {
                TotalProducts = totalProducts,
                TotalCategories = totalCategories,
                PopularCategories = popularCategories,
                Timestamp = DateTime.UtcNow
            };
        }

        private async Task<ConversationHistory> GetOrCreateConversationAsync(string sessionId)
        {
            var cacheKey = $"{CONVERSATION_PREFIX}{sessionId}";
            var cached = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cached))
            {
                return JsonSerializer.Deserialize<ConversationHistory>(cached) ?? new ConversationHistory();
            }

            return new ConversationHistory
            {
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow,
                Messages = new List<ChatMessage>()
            };
        }

        private async Task SaveConversationAsync(string sessionId, ConversationHistory conversation)
        {
            var cacheKey = $"{CONVERSATION_PREFIX}{sessionId}";
            var json = JsonSerializer.Serialize(conversation);

            await _cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CONVERSATION_TTL
            });
        }

        private async Task<string> AnalyzeIntentAsync(string message)
        {
            var prompt = """
                Analyse l'intention de l'utilisateur et retourne UN SEUL mot :
                - "recherche" : recherche de produits
                - "comparaison" : comparaison entre produits
                - "recommandation" : demande de recommandations
                - "specification" : demande de spécifications techniques
                - "prix" : question sur les prix ou promotions
                - "support" : question sur support, livraison, retour
                - "generique" : question générale
                
                Message : "{message}"
                """.Replace("{message}", message);

            var response = await _geminiService.AskAsync("Tu es un analyseur d'intention.", prompt);
            return response.Trim().ToLower();
        }

        private async Task<(List<ProductDto> Products, string SearchQuery)> GetRelevantProductsWithFiltersAsync(
            string query, string intent)
        {
            var allProducts = await _productService.GetAllAsync();
            var searchQuery = await ExtractSearchQueryAsync(query, intent);

            // Filtrer les produits
            var filteredProducts = FilterProducts(allProducts, searchQuery, intent);

            // Trier selon l'intention
            filteredProducts = SortProducts(filteredProducts, intent);

            return (filteredProducts.Take(12).ToList(), searchQuery);
        }

        private List<ProductDto> FilterProducts(List<ProductDto> products, string searchQuery, string intent)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return products.Take(20).ToList();

            var keywords = searchQuery.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return products
                .Where(p => keywords.Any(k =>
                    (p.Name ?? "").ToLower().Contains(k) ||
                    (p.Category ?? "").ToLower().Contains(k) ||
                    (p.Brand ?? "").ToLower().Contains(k) ||
                    (p.Description ?? "").ToLower().Contains(k)))
                .ToList();
        }

        private List<ProductDto> SortProducts(List<ProductDto> products, string intent)
        {
            return intent switch
            {
                "prix" => products.OrderBy(p => p.Price).ToList(),
                "recommandation" => products
                    .OrderByDescending(p => p.Rating)
                    .ThenByDescending(p => p.ReviewCount)
                    .ToList(),
                _ => products
                    .OrderByDescending(p => p.Rating)
                    .ThenBy(p => p.Price)
                    .ToList()
            };
        }

        private async Task<string> ExtractSearchQueryAsync(string message, string intent)
        {
            if (intent == "support" || intent == "generique")
                return string.Empty;

            var prompt = """
                Extrait uniquement les termes de recherche produit du message.
                Exclus : salutations, questions, mots vides.
                Retourne une phrase simple avec les mots-clés.
                
                Message : "{message}"
                Intention : {intent}
                """.Replace("{message}", message).Replace("{intent}", intent);

            var response = await _geminiService.AskAsync("Tu es un extracteur de mots-clés.", prompt);
            return response.Trim();
        }

        private async Task<string> BuildEnrichedContextAsync(List<ProductDto> products, string intent, string searchQuery)
        {
            var sb = new StringBuilder();

            sb.AppendLine("## CONTEXTE PRODUITS ##");

            if (products.Any())
            {
                sb.AppendLine($"Produits trouvés : {products.Count}");
                sb.AppendLine($"Requête : {searchQuery}");
                sb.AppendLine();

                foreach (var product in products.Take(5))
                {
                    sb.AppendLine($"### {product.Name} ({product.Brand})");
                    sb.AppendLine($"- Catégorie : {product.Category}");
                    sb.AppendLine($"- Prix : {product.Price:C}");
                    sb.AppendLine($"- Note : {product.Rating:F1}/5 ({product.ReviewCount} avis)");

                    if (!string.IsNullOrEmpty(product.Description))
                    {
                        var desc = product.Description.Length > 100
                            ? product.Description.Substring(0, 100) + "..."
                            : product.Description;
                        sb.AppendLine($"- Description : {desc}");
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("Aucun produit spécifique trouvé.");
                sb.AppendLine("Suggestions : smartphones, ordinateurs, écouteurs, montres connectées");
            }

            return sb.ToString();
        }

        private string BuildSystemPrompt(string intent)
        {
            var basePrompt = """
                Tu es un assistant commercial expert pour un site e-commerce.
                Tu es serviable, précis et professionnel.
                
                DIRECTIVES :
                1. Réponds en français
                2. Sois concis mais complet
                3. Utilise des emojis pertinents
                4. Si des produits sont mentionnés, parle-en spécifiquement
                5. Termine souvent par une question pour continuer la conversation
                
                STYLE : Amical mais professionnel, enthousiaste
                """;

            var intentPrompts = new Dictionary<string, string>
            {
                ["recherche"] = "L'utilisateur cherche des produits. Présente les options disponibles.",
                ["recommandation"] = "L'utilisateur veut des recommandations. Sois persuasif mais honnête.",
                ["comparaison"] = "L'utilisateur veut comparer. Sois objectif et détaillé.",
                ["prix"] = "L'utilisateur s'intéresse aux prix. Mentionne les promotions si disponibles.",
                ["support"] = "L'utilisateur a une question de service. Sois rassurant et précis."
            };

            var intentPrompt = intentPrompts.GetValueOrDefault(intent, "Réponds à la question de l'utilisateur.");

            return $"{basePrompt}\n\n{intentPrompt}";
        }

        private string BuildUserPrompt(string message, string context, ConversationHistory conversation)
        {
            var sb = new StringBuilder();

            // Ajouter le contexte historique récent
            if (conversation.Messages.Any())
            {
                sb.AppendLine("## HISTORIQUE RÉCENT ##");
                foreach (var msg in conversation.Messages.TakeLast(3))
                {
                    sb.AppendLine($"{msg.Role.ToUpper()} : {msg.Content}");
                }
                sb.AppendLine();
            }

            // Ajouter le contexte produit
            sb.AppendLine(context);
            sb.AppendLine();

            // Ajouter la question actuelle
            sb.AppendLine($"## QUESTION ACTUELLE ##");
            sb.AppendLine($"Utilisateur : {message}");

            return sb.ToString();
        }

        private async Task<List<string>> GenerateFollowUpSuggestionsAsync(string intent, List<ProductDto> products)
        {
            var suggestions = new List<string>();

            if (intent == "recherche" && products.Any())
            {
                suggestions.Add($"Comparer les {Math.Min(3, products.Count)} premiers produits");
                suggestions.Add("Voir plus d'options dans cette catégorie");
                suggestions.Add("Filtrer par prix");
            }
            else if (intent == "recommandation")
            {
                suggestions.Add("Meilleur rapport qualité-prix");
                suggestions.Add("Produits les mieux notés");
                suggestions.Add("Nouveautés");
            }
            else
            {
                suggestions.Add("Voir les promotions");
                suggestions.Add("Meilleures ventes");
                suggestions.Add("Questions fréquentes");
            }

            return suggestions;
        }
    }

    // Modèles de données
    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public string? Context { get; set; }
    }

    public class ChatResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<ProductDto> RecommendedProducts { get; set; } = new();
        public string SearchQuery { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public List<string> Suggestions { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class ConversationHistory
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<ChatMessage> Messages { get; set; } = new();
    }

    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty; // "user" or "assistant"
        public string Content { get; set; } = string.Empty;
        public List<ProductDto>? Products { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AssistantAnalytics
    {
        public int TotalProducts { get; set; }
        public int TotalCategories { get; set; }
        public List<CategoryStats> PopularCategories { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class CategoryStats
    {
        public string Name { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public decimal AvgPrice { get; set; }
        public double AvgRating { get; set; }
    }
}