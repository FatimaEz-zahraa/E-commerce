// Services/Interfaces/IHttpCartService.cs
using E_commerce.Models.DTOs;

namespace E_commerce.Services.Interfaces
{
    public interface IHttpCartService
    {
        Task<CartDto> GetCartAsync();
        Task<CartDto> AddItemAsync(Guid productId, int quantity = 1);
        Task<CartDto> UpdateQuantityAsync(Guid productId, int quantity);
        Task<CartDto> RemoveItemAsync(Guid productId);
        Task<CartDto> ClearCartAsync();
        Task<int> GetItemCountAsync();
        Task<decimal> GetTotalAsync();
        Task MergeCartAsync(string userId);
    }
}