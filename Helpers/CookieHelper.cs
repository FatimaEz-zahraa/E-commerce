using System.Text.Json;
using Microsoft.AspNetCore.Http;
using E_commerce.Models.DTOs;

namespace E_commerce.Helpers
{
    public static class CookieHelper
    {
        private const string CartCookieName = "ECOM_CART";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        // =========================
        // Récupérer le panier côté serveur (équivalent à GetCartFromCookie)
        // =========================
        public static CartCookieDto GetCart(HttpContext context) => GetOrCreateCart(context);

        public static CartCookieDto GetOrCreateCart(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (context.Request.Cookies.TryGetValue(CartCookieName, out var cookieValue) && !string.IsNullOrEmpty(cookieValue))
            {
                try
                {
                    return JsonSerializer.Deserialize<CartCookieDto>(cookieValue, JsonOptions) ?? new CartCookieDto();
                }
                catch
                {
                    return new CartCookieDto();
                }
            }
            return new CartCookieDto();
        }

        // =========================
        // Ajouter un produit au panier
        // =========================
        public static void AddProductToCart(HttpContext context, Guid productId, string name, decimal price, int quantity = 1, string? imageUrl = null, string? brand = null, string? category = null)
        {
            var cart = GetOrCreateCart(context);

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (existingItem != null)
                existingItem.Quantity += quantity;
            else
            {
                cart.Items.Add(new CartItemCookieDto
                {
                    ProductId = productId,
                    ProductName = name,
                    Price = price,
                    Quantity = quantity,
                    ImageUrl = imageUrl,
                    Brand = brand,
                    Category = category,
                    AddedAt = DateTime.UtcNow
                });
            }

            SaveCart(context, cart);
        }

        // =========================
        // Mettre à jour la quantité
        // =========================
        public static void UpdateCartQuantity(HttpContext context, Guid productId, int quantity)
        {
            var cart = GetOrCreateCart(context);
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item != null)
            {
                if (quantity <= 0) cart.Items.Remove(item);
                else item.Quantity = quantity;

                SaveCart(context, cart);
            }
        }

        // =========================
        // Retirer un produit
        // =========================
        public static void RemoveFromCart(HttpContext context, Guid productId)
        {
            var cart = GetOrCreateCart(context);
            if (cart.Items.RemoveAll(i => i.ProductId == productId) > 0)
                SaveCart(context, cart);
        }

        // =========================
        // Vider le panier
        // =========================
        public static void ClearCart(HttpContext context)
        {
            var options = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(-1),
                Path = "/"
            };
            context.Response.Cookies.Append(CartCookieName, "", options);
        }

        // =========================
        // Nombre total d'articles
        // =========================
        public static int GetCartItemCount(HttpContext context) => GetOrCreateCart(context).Items.Sum(i => i.Quantity);

        // =========================
        // Sauvegarder le panier côté cookie
        // =========================
        private static void SaveCart(HttpContext context, CartCookieDto cart)
        {
            cart.Items.RemoveAll(i => i.Quantity <= 0);

            var serializedCart = JsonSerializer.Serialize(cart, JsonOptions);

            var options = new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                Path = "/"
            };

            context.Response.Cookies.Append(CartCookieName, serializedCart, options);
        }

        // =========================
        // DEBUG : afficher le panier
        // =========================
        public static string DebugCart(HttpContext context)
        {
            try
            {
                var cart = GetOrCreateCart(context);

                var debugInfo = $"=== DEBUG CART COOKIE ===\n";
                debugInfo += $"URL: {context.Request.Path}\n";
                debugInfo += $"Items count: {cart.Items.Count}\n";
                debugInfo += $"Total quantity: {cart.Items.Sum(i => i.Quantity)}\n";
                debugInfo += $"Total price: {cart.Items.Sum(i => i.Price * i.Quantity):C}\n\n";

                foreach (var item in cart.Items)
                {
                    debugInfo += $"• {item.ProductName} x{item.Quantity} (ID={item.ProductId})\n";
                    debugInfo += $"  Prix: {item.Price:C} x {item.Quantity} = {item.Price * item.Quantity:C}\n";
                    debugInfo += $"  Marque: {item.Brand ?? "N/A"}\n";
                    debugInfo += $"  Catégorie: {item.Category ?? "N/A"}\n";
                    debugInfo += $"  Ajouté: {item.AddedAt:dd/MM/yyyy HH:mm}\n\n";
                }

                return debugInfo;
            }
            catch (Exception ex)
            {
                return $"ERREUR DebugCart: {ex.Message}";
            }
        }
    }

    // =========================
    // DTOs du cookie
    // =========================
    public class CartCookieDto
    {
        public List<CartItemCookieDto> Items { get; set; } = new List<CartItemCookieDto>();
        public decimal TotalAmount => Items.Sum(i => i.Price * i.Quantity);
        public int TotalItems => Items.Sum(i => i.Quantity);
    }

    public class CartItemCookieDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? ImageUrl { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public decimal TotalPrice => Price * Quantity;
    }
}
