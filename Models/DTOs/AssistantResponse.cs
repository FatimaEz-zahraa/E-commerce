// Models/DTOs/AssistantResponse.cs
namespace E_commerce.Models.DTOs
{
    /// <summary>
    /// Réponse structurée de l'assistant IA avec recommandations produits
    /// </summary>
    public class AssistantResponse
    {
        /// <summary>
        /// Réponse textuelle générée par l'assistant
        /// </summary>
        public string TextResponse { get; set; } = string.Empty;

        /// <summary>
        /// Liste des produits recommandés par l'assistant
        /// </summary>
        public List<ProductDto> RecommendedProducts { get; set; } = new List<ProductDto>();

        /// <summary>
        /// Requête de recherche originale de l'utilisateur
        /// </summary>
        public string SearchQuery { get; set; } = string.Empty;

        /// <summary>
        /// Horodatage de la réponse
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Nombre total de produits recommandés
        /// </summary>
        public int ProductCount { get; set; }

        /// <summary>
        /// Indique si des produits ont été trouvés et recommandés
        /// </summary>
        public bool HasProducts { get; set; }

        /// <summary>
        /// Métadonnées additionnelles (optionnel)
        /// Peut contenir des informations comme la confiance de la réponse, 
        /// les catégories détectées, etc.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Identifiant unique du message renvoyé par l'assistant (pour rattacher les cartes côté client)
        /// </summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
    }
}