// Services/FallbackAssistantService.cs
using E_commerce.Services.Interfaces;

namespace E_commerce.Services
{
    public class FallbackAssistantService 
    {
        private readonly ILogger<FallbackAssistantService> _logger;

        public FallbackAssistantService(ILogger<FallbackAssistantService> logger)
        {
            _logger = logger;
        }

        public async Task<string> AskAsync(string systemPrompt, string userPrompt, string? context = null)
        {
            _logger.LogInformation("Fallback assistant utilisé pour: {Prompt}", userPrompt);

            await Task.Delay(100);

            // Réponses de fallback simples
            var lowerPrompt = userPrompt.ToLower();

            if (lowerPrompt.Contains("bonjour") || lowerPrompt.Contains("salut") || lowerPrompt.Contains("hello"))
                return "Bonjour ! Comment puis-je vous aider aujourd'hui ?";

            if (lowerPrompt.Contains("smartphone") || lowerPrompt.Contains("iphone") || lowerPrompt.Contains("samsung"))
                return "Nous avons une large sélection de smartphones. Consultez notre catégorie 'Smartphones' pour voir les modèles disponibles.";

            if (lowerPrompt.Contains("ordinateur") || lowerPrompt.Contains("pc") || lowerPrompt.Contains("laptop"))
                return "Pour les ordinateurs, nous vous recommandons de consulter nos catégories 'Ordinateurs portables' et 'Ordinateurs de bureau'.";

            if (lowerPrompt.Contains("livraison") || lowerPrompt.Contains("expédition"))
                return "Nous proposons la livraison gratuite à partir de 50€ d'achat. Délais de livraison: 2-5 jours ouvrés.";

            if (lowerPrompt.Contains("prix") || lowerPrompt.Contains("coût") || lowerPrompt.Contains("tarif"))
                return "Les prix varient selon les produits. Vous pouvez filtrer par gamme de prix sur notre page produits.";

            return "Je suis désolé, l'assistant IA est temporairement indisponible. Vous pouvez parcourir nos catégories de produits ou contacter notre service client au 01 23 45 67 89.";
        }

        public async Task<string> GenerateProductDescriptionAsync(ProductDto product)
        {
            return $"""
                {product.Name} - {product.Brand}
                
                Découvrez le {product.Name} de la marque {product.Brand}, un excellent choix dans la catégorie {product.Category}.
                
                À seulement {product.Price:C}, ce produit offre un excellent rapport qualité-prix.
                
                {product.Description}
                
                Parfait pour un usage quotidien, ce produit répondra à toutes vos attentes.
                """;
        }

        public async Task<List<string>> ExtractSearchKeywordsAsync(string userQuery)
        {
            var stopWords = new HashSet<string>
            {
                "le", "la", "les", "un", "une", "des", "du", "de", "et",
                "ou", "où", "à", "au", "aux", "dans", "sur", "avec",
                "pour", "par", "est", "sont", "ces", "cette", "ce", "que",
                "qui", "quoi", "comment", "pourquoi", "quand"
            };

            return userQuery.ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Length > 2)
                .Where(word => !stopWords.Contains(word))
                .Distinct()
                .Take(5)
                .ToList();
        }
    }
}