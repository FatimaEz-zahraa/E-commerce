// Services/Implementations/HttpCartService.cs
using E_commerce.Helpers;
using E_commerce.Models.DTOs;
using E_commerce.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace E_commerce.Services.Implementations
{
    public class HttpCartService : IHttpCartService
    {
        private readonly ICartService _cartService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpCartService(ICartService cartService, IHttpContextAccessor httpContextAccessor)
        {
            _cartService = cartService;
            _httpContextAccessor = httpContextAccessor;
        }

        private HttpContext GetHttpContext()
        {
            return _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HTTP Context is not available");
        }

        private (string? userId, string cartId) GetIdentifiers()
        {
            var context = GetHttpContext();
            var userId = context.User.Identity?.IsAuthenticated == true
                ? context.User.Identity.Name
                : null;
            var cartId = CartHelper.GetOrCreateCartId(context);
            return (userId, cartId);
        }

        public async Task<CartDto> GetCartAsync()
        {
            var (userId, cartId) = GetIdentifiers();
            return await _cartService.GetCartAsync(userId, cartId);
        }

        public async Task<CartDto> AddItemAsync(Guid productId, int quantity = 1)
        {
            var (userId, cartId) = GetIdentifiers();
            await _cartService.AddToCartAsync(userId, cartId, productId, quantity);
            return await GetCartAsync();
        }

        public async Task<CartDto> UpdateQuantityAsync(Guid productId, int quantity)
        {
            var (userId, cartId) = GetIdentifiers();
            await _cartService.UpdateQuantityAsync(userId, cartId, productId, quantity);
            return await GetCartAsync();
        }

        public async Task<CartDto> RemoveItemAsync(Guid productId)
        {
            var (userId, cartId) = GetIdentifiers();
            await _cartService.RemoveFromCartAsync(userId, cartId, productId);
            return await GetCartAsync();
        }

        public async Task<CartDto> ClearCartAsync()
        {
            var (userId, cartId) = GetIdentifiers();
            await _cartService.ClearCartAsync(userId, cartId);
            return await GetCartAsync();
        }

        public async Task<int> GetItemCountAsync()
        {
            var (userId, cartId) = GetIdentifiers();
            return await _cartService.GetCartItemCountAsync(userId, cartId);
        }

        public async Task<decimal> GetTotalAsync()
        {
            var cart = await GetCartAsync();
            return cart.Total;
        }

        public async Task MergeCartAsync(string userId)
        {
            var cartId = CartHelper.GetOrCreateCartId(GetHttpContext());
            await _cartService.MergeCartAsync(userId, cartId);
        }
    }
}