// Controllers/Api/RagController.cs
using E_commerce.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace E_commerce.Controllers.Api
{
    [ApiController]
    [Route("api/rag")]
    public class RagController : ControllerBase
    {
        private readonly IRagService _ragService;
        private readonly ILogger<RagController> _logger;

        public RagController(
            IRagService ragService,
            ILogger<RagController> logger)
        {
            _ragService = ragService;
            _logger = logger;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> AskQuestion([FromBody] AskRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Question))
                {
                    return BadRequest(new { error = "La question est requise" });
                }

                _logger.LogInformation("Question RAG: {Question}", request.Question);

                // Utiliser la méthode structurée
                var response = await _ragService.AskWithProductsAsync(request.Question);

                return Ok(new
                {
                    success = true,
                    response = response.TextResponse,
                    messageId = response.MessageId,
                    recommendedProducts = response.RecommendedProducts.Select(p => new
                    {
                        id = p.Id,
                        name = p.Name,
                        price = p.Price,
                        oldPrice = p.OldPrice,
                        discountPercentage = p.DiscountPercentage,
                        imageUrl = !string.IsNullOrEmpty(p.ImageUrl) ? p.ImageUrl : "/images/placeholder.jpg",
                        category = p.Category,
                        brand = p.Brand,
                        rating = p.Rating,
                        reviewCount = p.ReviewCount,
                        description = p.Description,
                        stockQuantity = p.StockQuantity,
                        isActive = p.IsActive,
                        createdAt = p.CreatedAt.ToString("yyyy-MM-dd")
                    }).ToList(),
                    query = response.SearchQuery,
                    productCount = response.ProductCount,
                    hasProducts = response.HasProducts,
                    timestamp = response.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    source = "gemini-rag"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du traitement de la question RAG");

                return StatusCode(500, new
                {
                    success = false,
                    error = "Erreur interne",
                    message = ex.Message,
                    fallback = "Je rencontre des difficultés techniques. Veuillez réessayer.",
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
        }

        [HttpPost("recommend")]
        public async Task<IActionResult> GetRecommendations([FromBody] RecommendRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Query))
                {
                    return BadRequest(new { error = "La requête est requise" });
                }

                _logger.LogInformation("Recommandation: {Query}", request.Query);

                var recommendations = await _ragService.GetProductRecommendationsAsync(request.Query);

                return Ok(new
                {
                    success = true,
                    recommendations,
                    query = request.Query,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération de recommandations");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Erreur lors de la génération"
                });
            }
        }

        [HttpPost("extract-keywords")]
        public async Task<IActionResult> ExtractKeywords([FromBody] ExtractKeywordsRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Query))
                {
                    return BadRequest(new { error = "La requête est requise" });
                }

                var keywords = await _ragService.ExtractSearchKeywordsAsync(request.Query);

                return Ok(new
                {
                    success = true,
                    query = request.Query,
                    keywords,
                    count = keywords.Count,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'extraction des mots-clés");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Erreur lors de l'extraction"
                });
            }
        }

        [HttpGet("health")]
        public async Task<IActionResult> HealthCheck()
        {
            try
            {
                var config = HttpContext.RequestServices.GetService<IConfiguration>();
                var apiKey = config?["Gemini:ApiKey"];
                var isEnabled = config?.GetValue<bool>("Gemini:Enabled", true);

                // Tester le service
                var analytics = await _ragService.GetAnalyticsAsync();

                return Ok(new
                {
                    status = !string.IsNullOrEmpty(apiKey) && isEnabled == true ? "healthy" : "unconfigured",
                    service = "gemini-rag",
                    timestamp = DateTime.UtcNow,
                    hasApiKey = !string.IsNullOrEmpty(apiKey),
                    isEnabled = isEnabled,
                    analytics = analytics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du health check");
                return StatusCode(503, new
                {
                    status = "unhealthy",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpGet("suggestions")]
        public IActionResult GetSuggestions()
        {
            var suggestions = new[]
            {
                new {
                    text = "Quels sont vos meilleurs smartphones ?",
                    icon = "📱",
                    category = "téléphonie"
                },
                new {
                    text = "Recommandez-moi un ordinateur portable",
                    icon = "💻",
                    category = "informatique"
                },
                new {
                    text = "Livraison gratuite disponible ?",
                    icon = "🚚",
                    category = "service"
                },
                new {
                    text = "Produits écologiques disponibles",
                    icon = "🌱",
                    category = "durabilité"
                },
                new {
                    text = "Cadeaux high-tech populaires",
                    icon = "🎁",
                    category = "cadeaux"
                },
                new {
                    text = "Service client et garanties",
                    icon = "🛡️",
                    category = "service"
                },
                new {
                    text = "Promotions en cours",
                    icon = "🏷️",
                    category = "promotion"
                },
                new {
                    text = "Produits les mieux notés",
                    icon = "⭐",
                    category = "qualité"
                }
            };

            return Ok(new
            {
                success = true,
                suggestions,
                count = suggestions.Length,
                timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics()
        {
            try
            {
                var analytics = await _ragService.GetAnalyticsAsync();

                return Ok(new
                {
                    success = true,
                    analytics,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des analytics");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }
    }

    // Modèles de requête
    public class AskRequest
    {
        public string Question { get; set; } = string.Empty;
        public string? Context { get; set; }
    }

    public class RecommendRequest
    {
        public string Query { get; set; } = string.Empty;
    }

    public class ExtractKeywordsRequest
    {
        public string Query { get; set; } = string.Empty;
    }
}