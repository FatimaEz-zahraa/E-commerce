// Services/Interfaces/ICartService.cs
using E_commerce.Models.DTOs;

namespace E_commerce.Services.Interfaces
{
    public interface ICartService
    {
        // Récupérer le panier
        Task<CartDto> GetCartAsync(string? userId, string cartId);

        // Ajouter un produit au panier
        Task AddToCartAsync(string? userId, string cartId, Guid productId, int quantity = 1);

        // Mettre à jour la quantité d'un produit
        Task UpdateQuantityAsync(string? userId, string cartId, Guid productId, int quantity);

        // Retirer un produit du panier
        Task RemoveFromCartAsync(string? userId, string cartId, Guid productId);

        // Vider le panier
        Task ClearCartAsync(string? userId, string cartId);

        // Obtenir le nombre total d'articles dans le panier
        Task<int> GetCartItemCountAsync(string? userId, string cartId);

        // Fusionner le panier anonyme avec le panier utilisateur
        Task MergeCartAsync(string userId, string anonymousCartId);
    }
}