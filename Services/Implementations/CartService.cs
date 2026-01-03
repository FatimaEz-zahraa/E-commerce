using E_commerce.Data;
using E_commerce.Helpers;
using E_commerce.Models.DTOs;
using E_commerce.Models.Entities;
using E_commerce.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

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

        private HttpContext HttpContext =>
            _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext unavailable");

        // =========================
        // GET CART
        // =========================
        public async Task<CartDto> GetCartAsync(string? userId)
        {
            if (string.IsNullOrEmpty(userId))
                return await BuildGuestCartAsync();

            var cacheKey = $"cart_user_{userId}";
            if (_cache.TryGetValue(cacheKey, out CartDto cached))
                return cached;

            var cart = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var dto = MapToDto(cart);
            _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(30));
            return dto;
        }

        
        private async Task<CartDto> BuildGuestCartAsync()
        {
            var cookieCart = CookieHelper.GetOrCreateCart(HttpContext);
            if (!cookieCart.Items.Any()) return new CartDto();

            var productIds = cookieCart.Items.Select(i => i.ProductId).ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id) && p.IsActive)
                .ToListAsync();

            var items = new List<CartItemDto>();
            foreach (var cookieItem in cookieCart.Items)
            {
                var product = products.FirstOrDefault(p => p.Id == cookieItem.ProductId);
                if (product == null) continue; // Produit n'existe plus en DB

                items.Add(new CartItemDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Price = product.Price,
                    Quantity = Math.Min(cookieItem.Quantity, product.StockQuantity),
                    ImageUrl = product.ImageUrl,
                    Brand = product.Brand,
                    Category = product.Category,
                    IsAvailable = product.StockQuantity > 0,
                    MaxQuantity = product.StockQuantity
                });
            }

            var cartDto = new CartDto { Items = items };
            cartDto.CalculateTotals();
            return cartDto;
        }

        // =========================
        // ADD ITEM
        // =========================
        public async Task AddToCartAsync(string? userId, Guid productId, int quantity = 1)
        {
            Console.WriteLine($"DEBUG AddToCartAsync: userId={userId}, productId={productId}, quantity={quantity}");

            // SUPPRIMEZ les Include pour Brand et Category
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);

            if (product == null)
                throw new Exception($"Produit introuvable avec ID: {productId}");

            if (product.StockQuantity <= 0)
                throw new Exception($"Produit en rupture de stock: {product.Name}");

            quantity = Math.Min(quantity, product.StockQuantity);
            Console.WriteLine($"DEBUG: Produit trouvé - {product.Name}, Stock: {product.StockQuantity}, Qty ajoutée: {quantity}");

            if (string.IsNullOrEmpty(userId))
            {
                // Ajouter au cookie pour utilisateur non connecté
                Console.WriteLine($"DEBUG: Ajout au cookie (guest)");

                CookieHelper.AddProductToCart(
                    context: HttpContext,
                    productId: product.Id,
                    name: product.Name,
                    price: product.Price,
                    quantity: quantity,
                    imageUrl: product.ImageUrl,
                    brand: product.Brand, // SIMPLE : string direct
                    category: product.Category // SIMPLE : string direct
                );

                Console.WriteLine($"DEBUG: Cookie mis à jour");
                return;
            }

            // Pour utilisateur connecté - sauvegarder en base
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
            }

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                var newQuantity = item.Quantity + quantity;
                item.Quantity = Math.Min(newQuantity, product.StockQuantity);
                Console.WriteLine($"DEBUG: Produit existant - nouvelle quantité: {item.Quantity}");
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = productId,
                    Quantity = Math.Min(quantity, product.StockQuantity),
                    AddedAt = DateTime.UtcNow
                });
                Console.WriteLine($"DEBUG: Nouvel item ajouté");
            }

            cart.UpdatedAt = DateTime.UtcNow;
            _context.Carts.Update(cart);
            await _context.SaveChangesAsync();

            // Invalider le cache
            _cache.Remove($"cart_user_{userId}");

            Console.WriteLine($"DEBUG: Panier sauvegardé en DB");
        }

        // =========================
        // UPDATE QUANTITY
        // =========================
        public async Task UpdateQuantityAsync(string? userId, Guid productId, int quantity)
        {
            Console.WriteLine($"DEBUG UpdateQuantity: userId={userId}, productId={productId}, quantity={quantity}");

            if (quantity < 0)
                throw new ArgumentException("La quantité ne peut pas être négative");

            if (string.IsNullOrEmpty(userId))
            {
                CookieHelper.UpdateCartQuantity(HttpContext, productId, quantity);
                return;
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null) return;

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item == null) return;

            // Vérifier le stock
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product != null)
            {
                quantity = Math.Min(quantity, product.StockQuantity);
            }

            if (quantity <= 0)
            {
                cart.Items.Remove(item);
                Console.WriteLine($"DEBUG: Item supprimé (quantité 0)");
            }
            else
            {
                item.Quantity = quantity;
                Console.WriteLine($"DEBUG: Quantité mise à jour à {quantity}");
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _cache.Remove($"cart_user_{userId}");
        }

        // =========================
        // REMOVE ITEM
        // =========================
        public async Task RemoveFromCartAsync(string? userId, Guid productId)
        {
            Console.WriteLine($"DEBUG RemoveFromCart: userId={userId}, productId={productId}");

            if (string.IsNullOrEmpty(userId))
            {
                CookieHelper.RemoveFromCart(HttpContext, productId);
                return;
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            var item = cart?.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item == null) return;

            cart!.Items.Remove(item);
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _cache.Remove($"cart_user_{userId}");
        }

        // =========================
        // CLEAR CART
        // =========================
        public async Task ClearCartAsync(string? userId)
        {
            Console.WriteLine($"DEBUG ClearCart: userId={userId}");

            if (string.IsNullOrEmpty(userId))
            {
                CookieHelper.ClearCart(HttpContext);
                return;
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null) return;

            cart.Items.Clear();
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _cache.Remove($"cart_user_{userId}");
        }

        // =========================
        // COUNT ITEMS
        // =========================
        public async Task<int> GetCartItemCountAsync(string? userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                var Count = CookieHelper.GetCartItemCount(HttpContext);
                Console.WriteLine($"DEBUG GetCartItemCount (guest): {Count}");
                return Count;
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            var count = cart?.Items.Sum(i => i.Quantity) ?? 0;
            Console.WriteLine($"DEBUG GetCartItemCount (user): {count}");
            return count;
        }

        // =========================
        // MERGE COOKIE → USER
        // =========================
        public async Task MergeCookieCartToUserAsync(string userId)
        {
            Console.WriteLine($"DEBUG MergeCookieCartToUser pour userId: {userId}");

            var cookieCart = CookieHelper.GetOrCreateCart(HttpContext);
            if (!cookieCart.Items.Any())
            {
                Console.WriteLine("DEBUG: Cookie vide, rien à fusionner");
                return;
            }

            Console.WriteLine($"DEBUG: {cookieCart.Items.Count} items à fusionner");

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
            }

            foreach (var cookieItem in cookieCart.Items)
            {
                // Vérifier que le produit existe
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == cookieItem.ProductId && p.IsActive);

                if (product == null || product.StockQuantity <= 0)
                {
                    Console.WriteLine($"DEBUG: Produit {cookieItem.ProductId} non trouvé ou non disponible");
                    continue;
                }

                var existing = cart.Items.FirstOrDefault(i => i.ProductId == cookieItem.ProductId);
                if (existing != null)
                {
                    var newQuantity = existing.Quantity + cookieItem.Quantity;
                    existing.Quantity = Math.Min(newQuantity, product.StockQuantity);
                    Console.WriteLine($"DEBUG: Fusion item existant - nouvelle quantité: {existing.Quantity}");
                }
                else
                {
                    cart.Items.Add(new CartItem
                    {
                        ProductId = cookieItem.ProductId,
                        Quantity = Math.Min(cookieItem.Quantity, product.StockQuantity),
                        AddedAt = DateTime.UtcNow
                    });
                    Console.WriteLine($"DEBUG: Nouvel item fusionné: {cookieItem.ProductId}");
                }
            }

            cart.UpdatedAt = DateTime.UtcNow;
            _context.Carts.Update(cart);
            await _context.SaveChangesAsync();

            // Vider le cookie après fusion
            CookieHelper.ClearCart(HttpContext);
            _cache.Remove($"cart_user_{userId}");

            Console.WriteLine($"DEBUG: Fusion terminée - {cart.Items.Count} items dans le panier utilisateur");
        }

        // =========================
        // MAPPER ET CALCULS
        // =========================
        private CartDto MapToDto(Cart cart)
        {
            var dto = new CartDto
            {
                Id = cart.Id.ToString(),
                UserId = cart.UserId ?? "",
                CreatedAt = cart.CreatedAt,
                UpdatedAt = cart.UpdatedAt ?? DateTime.UtcNow,
                Items = new List<CartItemDto>()
            };

            foreach (var item in cart.Items)
            {
                if (item.Product != null && item.Product.IsActive)
                {
                    dto.Items.Add(new CartItemDto
                    {
                        ProductId = item.ProductId,
                        ProductName = item.Product.Name,
                        Price = item.Product.Price,
                        Quantity = Math.Min(item.Quantity, item.Product.StockQuantity),
                        ImageUrl = item.Product.ImageUrl,
                        // SIMPLE : Accéder directement aux strings
                        Brand = item.Product.Brand,
                        Category = item.Product.Category,
                        IsAvailable = item.Product.StockQuantity > 0,
                        MaxQuantity = Math.Min(item.Product.StockQuantity, 10)
                    });
                }
            }

            dto.CalculateTotals();
            Console.WriteLine($"DEBUG MapToDto: {dto.Items.Count} items, Total: {dto.Total:C}");
            return dto;
        }

        // =========================
        // METHODE UTILITAIRE POUR DEBUG
        // =========================
        public async Task<string> DebugCartAsync(string? userId)
        {
            var cookieCart = CookieHelper.GetOrCreateCart(HttpContext);

            var debugInfo = $"=== DEBUG CART ===\n";
            debugInfo += $"User ID: {userId ?? "Guest"}\n";
            debugInfo += $"Cookie items: {cookieCart.Items.Count}\n";

            foreach (var item in cookieCart.Items)
            {
                debugInfo += $"  - {item.ProductId}: {item.ProductName} x{item.Quantity}\n";
            }

            if (!string.IsNullOrEmpty(userId))
            {
                var dbCart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                debugInfo += $"DB Cart items: {dbCart?.Items.Count ?? 0}\n";
                if (dbCart != null)
                {
                    foreach (var item in dbCart.Items)
                    {
                        debugInfo += $"  - {item.ProductId}: x{item.Quantity}\n";
                    }
                }
            }

            return debugInfo;
        }

        // =========================
        // DEBUG PRODUCT STRUCTURE
        // =========================
        public async Task<string> DebugProductStructure(Guid productId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
                return "Produit non trouvé";

            var debug = $"=== DEBUG PRODUCT STRUCTURE ===\n";
            debug += $"Product ID: {product.Id}\n";
            debug += $"Product Name: {product.Name}\n";
            debug += $"Brand: {product.Brand ?? "(null)"}\n";
            debug += $"Brand type: {product.Brand?.GetType().Name ?? "null"}\n";
            debug += $"Category: {product.Category ?? "(null)"}\n";
            debug += $"Category type: {product.Category?.GetType().Name ?? "null"}\n";

            return debug;
        }
    }
}