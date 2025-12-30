// Services/Implementations/CartService.cs
using E_commerce.Data;
using E_commerce.Helpers;
using E_commerce.Models.DTOs;
using E_commerce.Models.Entities;
using E_commerce.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http;

namespace E_commerce.Services.Implementations
{
    public class CartService : ICartService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IProductService _productService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CartService(
            AppDbContext context,
            IMemoryCache cache,
            IProductService productService,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _cache = cache;
            _productService = productService;
            _httpContextAccessor = httpContextAccessor;
        }

        private HttpContext GetHttpContext()
        {
            return _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HTTP Context is not available");
        }

        public async Task<CartDto> GetCartAsync(string? userId, string cartId)
        {
            // Utilisateur anonyme : retourner le cookie
            if (string.IsNullOrEmpty(userId))
            {
                return CookieHelper.GetOrCreateCart(GetHttpContext());
            }

            // Utilisateur connecté : vérifier le cache d'abord
            var cacheKey = $"cart_user_{userId}";
            if (_cache.TryGetValue(cacheKey, out CartDto cachedCart))
            {
                return cachedCart;
            }

            // Récupérer depuis la base de données
            var cart = await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            CartDto cartDto;

            if (cart == null)
            {
                // Créer un nouveau panier en base
                cart = new Cart
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();

                cartDto = new CartDto
                {
                    Id = cart.Id.ToString(),
                    UserId = cart.UserId,
                    Items = new List<CartItemDto>(),
                    CreatedAt = cart.CreatedAt
                };
            }
            else
            {
                // Mapper l'entité vers DTO
                cartDto = MapToDto(cart);
            }

            // Mettre en cache pour 30 minutes
            _cache.Set(cacheKey, cartDto, TimeSpan.FromMinutes(30));
            return cartDto;
        }

        public async Task AddToCartAsync(string? userId, string cartId, Guid productId, int quantity = 1)
        {
            if (quantity <= 0)
                throw new ArgumentException("La quantité doit être supérieure à 0", nameof(quantity));

            // Récupérer le produit
            var product = await _productService.GetByIdAsync(productId);
            if (product == null)
                throw new ArgumentException("Produit non trouvé", nameof(productId));

            // Vérifier le stock
            if (product.StockQuantity <= 0)
                throw new InvalidOperationException("Produit en rupture de stock");

            var availableQuantity = Math.Min(quantity, Math.Min(product.StockQuantity, 10));

            // Utilisateur anonyme : cookie
            if (string.IsNullOrEmpty(userId))
            {
                var cartItem = new CartItemDto
                {
                    ProductId = productId,
                    ProductName = product.Name,
                    Price = product.Price,
                    Quantity = availableQuantity,
                    ImageUrl = product.ImageUrl,
                    Brand = product.Brand,
                    Category = product.Category,
                    IsAvailable = product.StockQuantity > 0,
                    MaxQuantity = Math.Min(product.StockQuantity, 10)
                };

                CookieHelper.AddToCart(GetHttpContext(), cartItem);
                return;
            }

            // Utilisateur connecté : base de données
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
            }

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (existingItem != null)
            {
                existingItem.Quantity += availableQuantity;
                existingItem.Quantity = Math.Min(existingItem.Quantity, Math.Min(product.StockQuantity, 10));
                existingItem.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Quantity = availableQuantity,
                    AddedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Invalider le cache
            _cache.Remove($"cart_user_{userId}");
        }

        public async Task UpdateQuantityAsync(string? userId, string cartId, Guid productId, int quantity)
        {
            if (quantity < 0)
                throw new ArgumentException("La quantité ne peut pas être négative", nameof(quantity));

            // Récupérer le produit pour vérifier le stock
            var product = await _productService.GetByIdAsync(productId);
            if (product == null)
                throw new ArgumentException("Produit non trouvé", nameof(productId));

            var maxQuantity = Math.Min(product.StockQuantity, 10);
            var finalQuantity = Math.Min(quantity, maxQuantity);

            // Utilisateur anonyme : cookie
            if (string.IsNullOrEmpty(userId))
            {
                CookieHelper.UpdateCartQuantity(GetHttpContext(), productId, finalQuantity);
                return;
            }

            // Utilisateur connecté : base de données
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
                return;

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                if (finalQuantity == 0)
                {
                    cart.Items.Remove(item);
                }
                else
                {
                    item.Quantity = finalQuantity;
                    item.UpdatedAt = DateTime.UtcNow;
                }

                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Invalider le cache
                _cache.Remove($"cart_user_{userId}");
            }
        }

        public async Task RemoveFromCartAsync(string? userId, string cartId, Guid productId)
        {
            // Utilisateur anonyme : cookie
            if (string.IsNullOrEmpty(userId))
            {
                CookieHelper.RemoveFromCart(GetHttpContext(), productId);
                return;
            }

            // Utilisateur connecté : base de données
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart != null)
            {
                var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                if (item != null)
                {
                    cart.Items.Remove(item);
                    cart.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Invalider le cache
                    _cache.Remove($"cart_user_{userId}");
                }
            }
        }

        public async Task ClearCartAsync(string? userId, string cartId)
        {
            // Utilisateur anonyme : cookie
            if (string.IsNullOrEmpty(userId))
            {
                CookieHelper.ClearCart(GetHttpContext());
                return;
            }

            // Utilisateur connecté : base de données
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart != null)
            {
                cart.Items.Clear();
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Invalider le cache
                _cache.Remove($"cart_user_{userId}");
            }
        }

        public async Task<int> GetCartItemCountAsync(string? userId, string cartId)
        {
            // Utilisateur anonyme : cookie
            if (string.IsNullOrEmpty(userId))
            {
                return CookieHelper.GetCartItemCount(GetHttpContext());
            }

            // Utilisateur connecté : cache ou base de données
            var cacheKey = $"cart_user_{userId}";
            if (_cache.TryGetValue(cacheKey, out CartDto cachedCart))
            {
                return cachedCart.TotalItems;
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            return cart?.Items.Sum(i => i.Quantity) ?? 0;
        }

        public async Task MergeCartAsync(string userId, string anonymousCartId)
        {
            // Récupérer le panier du cookie
            var cookieCart = CookieHelper.GetOrCreateCart(GetHttpContext());

            // Si le cookie est vide, rien à faire
            if (!cookieCart.Items.Any())
                return;

            // Récupérer le panier utilisateur ou en créer un nouveau
            var userCart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (userCart == null)
            {
                userCart = new Cart
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(userCart);
            }

            // Fusionner les articles
            foreach (var cookieItem in cookieCart.Items)
            {
                var existingItem = userCart.Items.FirstOrDefault(i => i.ProductId == cookieItem.ProductId);
                if (existingItem != null)
                {
                    // Mettre à jour la quantité
                    existingItem.Quantity += cookieItem.Quantity;
                    existingItem.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Ajouter un nouvel article
                    userCart.Items.Add(new CartItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = cookieItem.ProductId,
                        Quantity = cookieItem.Quantity,
                        AddedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            userCart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Vider le cookie
            CookieHelper.ClearCart(GetHttpContext());

            // Invalider le cache
            _cache.Remove($"cart_user_{userId}");
        }

        private CartDto MapToDto(Cart cart)
        {
            var items = new List<CartItemDto>();

            foreach (var item in cart.Items)
            {
                var product = item.Product;
                if (product == null || !product.IsActive)
                    continue;

                items.Add(new CartItemDto
                {
                    ProductId = item.ProductId,
                    ProductName = product.Name,
                    Price = product.Price,
                    Quantity = item.Quantity,
                    ImageUrl = product.ImageUrl,
                    Brand = product.Brand,
                    Category = product.Category,
                    IsAvailable = product.StockQuantity > 0,
                    MaxQuantity = Math.Min(product.StockQuantity, 10)
                });
            }

            return new CartDto
            {
                Id = cart.Id.ToString(),
                UserId = cart.UserId,
                Items = items,
                CreatedAt = cart.CreatedAt,
                UpdatedAt = cart.UpdatedAt
            };
        }
    }
}