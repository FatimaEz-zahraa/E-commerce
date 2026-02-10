// Pages/Shared/RagPageModel.cs
using E_commerce.Models.DTOs;
using E_commerce.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace E_commerce.Pages.Shared
{
    /// <summary>
    /// Classe de base PageModel pour toutes les pages avec fonctionnalité d'assistant IA
    /// Fournit des handlers automatiques pour les requêtes AJAX de l'assistant
    /// </summary>
    public class RagPageModel : PageModel
    {
        protected readonly IRagService _ragService;
        protected readonly IProductService _productService;
        protected readonly ILogger _logger;

        public RagPageModel(
            IRagService ragService,
            IProductService productService,
            ILogger logger)
        {
            _ragService = ragService;
            _productService = productService;
            _logger = logger;
        }

        /// <summary>
        /// Modèle pour les requêtes de l'assistant
        /// </summary>
        public class AssistantQuestionRequest
        {
            [Required(ErrorMessage = "La question est requise")]
            [StringLength(500, ErrorMessage = "La question ne peut pas dépasser 500 caractères")]
            public string Question { get; set; } = string.Empty;
        }

        /// <summary>
        /// Handler POST pour poser une question à l'assistant
        /// Accessible via: /VotrePage?handler=AskAssistant
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> OnPostAskAssistantAsync([FromBody] AssistantQuestionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        error = "Question invalide",
                        message = "Veuillez fournir une question valide."
                    });
                }

                _logger.LogInformation("Question RAG reçue: {Question}", request.Question);

                // Utiliser le service RAG avec réponse structurée
                var response = await _ragService.AskWithProductsAsync(request.Question);

                // Formatter les produits pour le JSON
                var productsFormatted = response.RecommendedProducts.Select(p => new
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
                    description = TruncateDescription(p.Description, 100),
                    stockQuantity = p.StockQuantity,
                    isActive = p.IsActive
                }).ToList();

                return new JsonResult(new
                {
                    success = true,
                    response = response.TextResponse,
                    recommendedProducts = productsFormatted,
                    productCount = response.ProductCount,
                    hasProducts = response.HasProducts,
                    timestamp = response.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    query = response.SearchQuery
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du traitement de la question assistant");

                return new JsonResult(new
                {
                    success = false,
                    error = "Erreur serveur",
                    message = "Je rencontre des difficultés techniques. Veuillez réessayer dans quelques instants."
                })
                {
                    StatusCode = 500
                };
            }
        }

        /// <summary>
        /// Handler GET pour obtenir les suggestions de questions
        /// Accessible via: /VotrePage?handler=Suggestions
        /// </summary>
        [HttpGet]
        public IActionResult OnGetSuggestions()
        {
            try
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
                        text = "Produits en promotion",
                        icon = "🏷️",
                        category = "promotion"
                    },
                    new {
                        text = "Produits les mieux notés",
                        icon = "⭐",
                        category = "qualité"
                    },
                    new {
                        text = "Nouveautés de la semaine",
                        icon = "🎁",
                        category = "nouveautés"
                    }
                };

                return new JsonResult(new
                {
                    success = true,
                    suggestions,
                    count = suggestions.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des suggestions");

                return new JsonResult(new
                {
                    success = false,
                    error = "Erreur lors de la récupération des suggestions"
                })
                {
                    StatusCode = 500
                };
            }
        }

        /// <summary>
        /// Handler GET pour obtenir les analytics de l'assistant
        /// Accessible via: /VotrePage?handler=Analytics
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> OnGetAnalyticsAsync()
        {
            try
            {
                var analytics = await _ragService.GetAnalyticsAsync();

                return new JsonResult(new
                {
                    success = true,
                    analytics,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des analytics");

                return new JsonResult(new
                {
                    success = false,
                    error = ex.Message
                })
                {
                    StatusCode = 500
                };
            }
        }

        /// <summary>
        /// Méthode utilitaire pour tronquer les descriptions
        /// </summary>
        protected string TruncateDescription(string? description, int maxLength)
        {
            if (string.IsNullOrEmpty(description))
                return "";

            return description.Length <= maxLength
                ? description
                : description.Substring(0, maxLength) + "...";
        }
    }
}