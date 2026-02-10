// Services/Interfaces/IRagService.cs
using E_commerce.Models.DTOs;

namespace E_commerce.Services.Interfaces
{
    /// <summary>
    /// Interface pour le service RAG (Retrieval-Augmented Generation)
    /// Fournit des fonctionnalités d'assistant IA avec contexte produits
    /// </summary>
    public interface IRagService
    {
        /// <summary>
        /// Pose une question simple au système RAG
        /// </summary>
        Task<string> AskAsync(string question);

        /// <summary>
        /// Pose une question avec contexte produit spécifique
        /// </summary>
        Task<string> AskWithProductContextAsync(string question, ProductDto? currentProduct = null);

        /// <summary>
        /// Pose une question et retourne une réponse structurée avec produits recommandés
        /// </summary>
        Task<AssistantResponse> AskWithProductsAsync(string question);

        /// <summary>
        /// Génère des recommandations de produits formatées
        /// </summary>
        Task<string> GetProductRecommendationsAsync(string query);

        /// <summary>
        /// Extrait les mots-clés pertinents d'une requête utilisateur
        /// </summary>
        Task<List<string>> ExtractSearchKeywordsAsync(string userQuery);

        /// <summary>
        /// Génère une description produit enrichie par IA
        /// </summary>
        Task<string> GenerateEnhancedProductDescriptionAsync(ProductDto product);

        /// <summary>
        /// Compare plusieurs produits et génère un rapport
        /// </summary>
        Task<string> CompareProductsAsync(List<ProductDto> products);

        /// <summary>
        /// Récupère les analytics du service RAG
        /// </summary>
        Task<Dictionary<string, object>> GetAnalyticsAsync();
    }
}