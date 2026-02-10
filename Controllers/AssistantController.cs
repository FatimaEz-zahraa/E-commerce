// Controllers/AssistantController.cs
using E_commerce.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace E_commerce.Controllers
{
    [ApiController]
    [Route("api/assistant")]
    [AllowAnonymous] // Permettre à tous d'utiliser l'assistant
    public class AssistantController : ControllerBase
    {
        private readonly IRagService _ragService;
        private readonly IProductService _productService;
        private readonly ILogger<AssistantController> _logger;

        public AssistantController(
            IRagService ragService,
            IProductService productService,
            ILogger<AssistantController> logger)
        {
            _ragService = ragService;
            _productService = productService;
            _logger = logger;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> AskAssistant([FromBody] AssistantRequest request)
        {
            try
            {
                // Vérifier si la question concerne un produit spécifique
                var productMatch = await ExtractProductFromQuestion(request.Question);

                string response;
                if (productMatch != null)
                {
                    // Utiliser le contexte produit
                    response = await _ragService.AskWithProductContextAsync(
                        request.Question,
                        productMatch);
                }
                else
                {
                    // Utiliser le RAG standard
                    response = await _ragService.AskAsync(request.Question);
                }

                // Formater la réponse
                var formattedResponse = FormatResponse(response);

                return Ok(new AssistantResponse
                {
                    Success = true,
                    Response = formattedResponse,
                    Suggestions = await GenerateSuggestions(request.Question)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans l'assistant");
                return Ok(new AssistantResponse
                {
                    Success = false,
                    Response = "Je rencontre des difficultés techniques. Pouvez-vous reformuler votre question?",
                    Suggestions = new[] { "Quels sont vos produits populaires?", "Avez-vous des promotions?", "Comment passer commande?" }
                });
            }
        }

        [HttpPost("quick-actions")]
        public async Task<IActionResult> GetQuickActions()
        {
            var actions = new[]
            {
                new QuickAction {
                    Icon = "📱",
                    Title = "Smartphones",
                    Query = "Montrez-moi vos smartphones"
                },
                new QuickAction {
                    Icon = "💻",
                    Title = "Ordinateurs",
                    Query = "Quels ordinateurs portables avez-vous?"
                },
                new QuickAction {
                    Icon = "🎧",
                    Title = "Audio",
                    Query = "Écouteurs et casques audio"
                },
                new QuickAction {
                    Icon = "🏷️",
                    Title = "Promotions",
                    Query = "Quelles sont les promotions du moment?"
                },
                new QuickAction {
                    Icon = "🚚",
                    Title = "Livraison",
                    Query = "Informations sur la livraison"
                },
                new QuickAction {
                    Icon = "↩️",
                    Title = "Retours",
                    Query = "Politique de retour des produits"
                }
            };

            return Ok(actions);
        }

        private async Task<ProductDto?> ExtractProductFromQuestion(string question)
        {
            // Logique simple pour détecter les références produits
            var products = await _productService.GetAllAsync();

            foreach (var product in products.Take(50)) // Limiter la recherche
            {
                if (question.Contains(product.Name, StringComparison.OrdinalIgnoreCase) ||
                    question.Contains(product.Category ?? "", StringComparison.OrdinalIgnoreCase) ||
                    question.Contains(product.Brand ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    return product;
                }
            }

            return null;
        }

        private string FormatResponse(string response)
        {
            // Ajouter des emojis et formater
            response = response
                .Replace("smartphone", "📱 smartphone")
                .Replace("ordinateur", "💻 ordinateur")
                .Replace("livraison", "🚚 livraison")
                .Replace("prix", "💰 prix")
                .Replace("promotion", "🏷️ promotion")
                .Replace("qualité", "⭐ qualité")
                .Replace("recommand", "👍 recommand");

            return response;
        }

        private async Task<string[]> GenerateSuggestions(string question)
        {
            var suggestions = new List<string>();

            if (question.Contains("smartphone", StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add("Comparer les modèles iPhone et Android");
                suggestions.Add("Smartphones avec la meilleure autonomie");
                suggestions.Add("Smartphones à moins de 500€");
            }
            else if (question.Contains("ordinateur", StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add("PC portable pour gaming");
                suggestions.Add("Ordinateurs légers pour déplacement");
                suggestions.Add("Configurations recommandées pour bureautique");
            }
            else
            {
                suggestions.Add("Quels sont vos meilleures ventes?");
                suggestions.Add("Produits avec livraison gratuite");
                suggestions.Add("Nouveautés du mois");
            }

            return suggestions.ToArray();
        }
    }

    public class AssistantRequest
    {
        [Required]
        [StringLength(500)]
        public string Question { get; set; } = string.Empty;

        public string? SessionId { get; set; }
    }

    public class AssistantResponse
    {
        public bool Success { get; set; }
        public string Response { get; set; } = string.Empty;
        public string[] Suggestions { get; set; } = Array.Empty<string>();
        public Product[]? RelatedProducts { get; set; }
    }

    public class QuickAction
    {
        public string Icon { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
    }
}