using E_commerce.Models.DTOs;

namespace E_commerce.Services.Interfaces
{
    public interface ICartService
    {
        Task<CartDto> GetCartAsync(string? userId);
        Task AddToCartAsync(string? userId, Guid productId, int quantity = 1);
        Task UpdateQuantityAsync(string? userId, Guid productId, int quantity);
        Task RemoveFromCartAsync(string? userId, Guid productId);
        Task ClearCartAsync(string? userId);
        Task<int> GetCartItemCountAsync(string? userId);
        Task MergeCookieCartToUserAsync(string userId);
    }
}