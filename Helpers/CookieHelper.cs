// Helpers/CookieHelper.cs
using System.Text.Json;
using E_commerce.Models.DTOs;

namespace E_commerce.Helpers
{
    public static class CookieHelper
    {
        private const string CartCookieName = "Cart";
        private const string CartIdCookieName = "CartId"; // Pour l'ancienne méthode

        // ==============================================
        // NOUVEAU SYSTÈME : Panier complet dans le cookie
        // ==============================================

        public static CartDto GetOrCreateCart(HttpContext context)
        {
            var cartJson = context.Request.Cookies[CartCookieName];

            if (string.IsNullOrEmpty(cartJson))
            {
                return CreateNewCart(context);
            }

            try
            {
                var options = GetJsonSerializerOptions();
                var cart = JsonSerializer.Deserialize<CartDto>(cartJson, options);

                // S'assurer que le cart n'est pas null
                if (cart == null)
                {
                    return CreateNewCart(context);
                }

                // S'assurer que les items ne sont pas null
                cart.Items ??= new List<CartItemDto>();

                return cart;
            }
            catch (JsonException)
            {
                return CreateNewCart(context);
            }
        }

        private static CartDto CreateNewCart(HttpContext context)
        {
            var newCart = new CartDto
            {
                Id = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                Items = new List<CartItemDto>()
            };

            SaveCart(context, newCart);
            return newCart;
        }

        public static void SaveCart(HttpContext context, CartDto cart)
        {
            cart.UpdatedAt = DateTime.UtcNow;

            var options = GetJsonSerializerOptions();
            var cartJson = JsonSerializer.Serialize(cart, options);

            context.Response.Cookies.Append(CartCookieName, cartJson, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(30),
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(30)
            });
        }

        private static JsonSerializerOptions GetJsonSerializerOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                PropertyNameCaseInsensitive = true
            };
        }

        // Méthodes simplifiées pour l'ajout direct
        public static void AddToCart(HttpContext context, CartItemDto item)
        {
            var cart = GetOrCreateCart(context);
            cart.AddItem(item);
            SaveCart(context, cart);
        }

        public static void UpdateCartQuantity(HttpContext context, Guid productId, int quantity)
        {
            var cart = GetOrCreateCart(context);
            cart.UpdateQuantity(productId, quantity);
            SaveCart(context, cart);
        }

        public static void RemoveFromCart(HttpContext context, Guid productId)
        {
            var cart = GetOrCreateCart(context);
            cart.RemoveItem(productId);
            SaveCart(context, cart);
        }

        public static void ClearCart(HttpContext context)
        {
            var cart = GetOrCreateCart(context);
            cart.Clear();
            SaveCart(context, cart);
        }

        public static int GetCartItemCount(HttpContext context)
        {
            var cart = GetOrCreateCart(context);
            return cart.TotalItems;
        }

        public static decimal GetCartTotal(HttpContext context)
        {
            var cart = GetOrCreateCart(context);
            return cart.Total;
        }

        public static bool IsProductInCart(HttpContext context, Guid productId)
        {
            var cart = GetOrCreateCart(context);
            return cart.Items.Any(i => i.ProductId == productId);
        }

        public static int GetProductQuantity(HttpContext context, Guid productId)
        {
            var cart = GetOrCreateCart(context);
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            return item?.Quantity ?? 0;
        }

        // ==============================================
        // ANCIEN SYSTÈME : Migration et compatibilité
        // ==============================================

        public static void MigrateOldCartToNew(HttpContext context)
        {
            var oldCartId = context.Request.Cookies[CartIdCookieName];
            if (!string.IsNullOrEmpty(oldCartId) && !context.Request.Cookies.ContainsKey(CartCookieName))
            {
                // Créer un nouveau panier avec l'ancien ID
                var newCart = new CartDto
                {
                    Id = oldCartId,
                    CreatedAt = DateTime.UtcNow,
                    Items = new List<CartItemDto>()
                };

                SaveCart(context, newCart);
                // Supprimer l'ancien cookie
                context.Response.Cookies.Delete(CartIdCookieName);
            }
        }

        public static CartDto GetOrCreateCartWithMigration(HttpContext context)
        {
            // Migrer d'abord si nécessaire
            MigrateOldCartToNew(context);
            // Puis utiliser le nouveau système
            return GetOrCreateCart(context);
        }

        // ==============================================
        // MÉTHODES DE L'ANCIEN SYSTÈME (pour compatibilité)
        // ==============================================

        public static string GetOrCreateCartId(HttpContext context)
        {
            var cartId = context.Request.Cookies[CartIdCookieName];

            if (string.IsNullOrEmpty(cartId))
            {
                cartId = Guid.NewGuid().ToString();
                context.Response.Cookies.Append(CartIdCookieName, cartId, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTime.UtcNow.AddDays(30),
                    IsEssential = true
                });
            }

            return cartId;
        }

        public static string GetCartId(HttpContext context)
        {
            var cart = GetOrCreateCart(context);
            return cart.Id;
        }

        public static void RemoveCartCookie(HttpContext context)
        {
            context.Response.Cookies.Delete(CartIdCookieName);
        }

        public static void ClearAllCartCookies(HttpContext context)
        {
            context.Response.Cookies.Delete(CartCookieName);
            context.Response.Cookies.Delete(CartIdCookieName);
        }

        // ==============================================
        // MÉTHODES UTILITAIRES SUPPLEMENTAIRES
        // ==============================================

        /// <summary>
        /// Ajoute un produit au panier avec toutes les informations nécessaires
        /// </summary>
        public static void AddProductToCart(HttpContext context, Guid productId, string productName,
                                            decimal price, int quantity = 1, string? imageUrl = null,
                                            string? brand = null, string? category = null)
        {
            var cartItem = new CartItemDto
            {
                ProductId = productId,
                ProductName = productName,
                Price = price,
                Quantity = quantity,
                ImageUrl = imageUrl,
                Brand = brand,
                Category = category,
                IsAvailable = true,
                MaxQuantity = 10 // Valeur par défaut
            };

            AddToCart(context, cartItem);
        }

        /// <summary>
        /// Vérifie si le panier est vide
        /// </summary>
        public static bool IsCartEmpty(HttpContext context)
        {
            var cart = GetOrCreateCart(context);
            return !cart.Items.Any();
        }

        /// <summary>
        /// Obtient le nombre de produits différents (pas la quantité totale)
        /// </summary>
        public static int GetProductCount(HttpContext context)
        {
            var cart = GetOrCreateCart(context);
            return cart.Items.Count;
        }

        /// <summary>
        /// Obtient le sous-total du panier (sans livraison ni taxes)
        /// </summary>
        public static decimal GetCartSubtotal(HttpContext context)
        {
            var cart = GetOrCreateCart(context);
            return cart.Subtotal;
        }

        /// <summary>
        /// Obtient les frais de livraison
        /// </summary>
        public static decimal GetShippingCost(HttpContext context)
        {
            var cart = GetOrCreateCart(context);
            return cart.ShippingCost;
        }

        /// <summary>
        /// Obtient le montant des taxes
        /// </summary>
        public static decimal GetTaxAmount(HttpContext context)
        {
            var cart = GetOrCreateCart(context);
            return cart.Tax;
        }

        /// <summary>
        /// Copie le panier du cookie vers un autre cookie (pour backup)
        /// </summary>
        public static void BackupCart(HttpContext context, string backupCookieName = "CartBackup")
        {
            var cart = GetOrCreateCart(context);
            var options = GetJsonSerializerOptions();
            var cartJson = JsonSerializer.Serialize(cart, options);

            context.Response.Cookies.Append(backupCookieName, cartJson, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7),
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(7)
            });
        }

        /// <summary>
        /// Restaure le panier depuis un backup
        /// </summary>
        public static bool RestoreCart(HttpContext context, string backupCookieName = "CartBackup")
        {
            var backupJson = context.Request.Cookies[backupCookieName];
            if (string.IsNullOrEmpty(backupJson))
                return false;

            try
            {
                var options = GetJsonSerializerOptions();
                var backupCart = JsonSerializer.Deserialize<CartDto>(backupJson, options);

                if (backupCart != null)
                {
                    SaveCart(context, backupCart);
                    // Supprimer le backup après restauration
                    context.Response.Cookies.Delete(backupCookieName);
                    return true;
                }
            }
            catch (JsonException)
            {
                // En cas d'erreur, on ignore simplement
            }

            return false;
        }

        /// <summary>
        /// Vide tous les cookies liés au panier (nouveau et ancien système)
        /// </summary>
        public static void ResetCart(HttpContext context)
        {
            ClearAllCartCookies(context);

            // Créer un nouveau panier vide
            var newCart = new CartDto
            {
                Id = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                Items = new List<CartItemDto>()
            };

            SaveCart(context, newCart);
        }

        /// <summary>
        /// Exporte le panier en JSON (pour debug ou sauvegarde)
        /// </summary>
        public static string ExportCartToJson(HttpContext context)
        {
            var cart = GetOrCreateCart(context);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            return JsonSerializer.Serialize(cart, options);
        }

        /// <summary>
        /// Importe un panier depuis du JSON
        /// </summary>
        public static bool ImportCartFromJson(HttpContext context, string cartJson)
        {
            try
            {
                var options = GetJsonSerializerOptions();
                var cart = JsonSerializer.Deserialize<CartDto>(cartJson, options);

                if (cart != null)
                {
                    SaveCart(context, cart);
                    return true;
                }
            }
            catch (JsonException)
            {
                // En cas d'erreur, on retourne false
            }

            return false;
        }

        /// <summary>
        /// Obtient un résumé du panier pour affichage
        /// </summary>
        public static CartSummaryDto GetCartSummary(HttpContext context)
        {
            var cart = GetOrCreateCart(context);

            return new CartSummaryDto
            {
                ItemCount = cart.TotalItems,
                ProductCount = cart.Items.Count,
                Subtotal = cart.Subtotal,
                ShippingCost = cart.ShippingCost,
                Tax = cart.Tax,
                Total = cart.Total,
                IsEligibleForFreeShipping = cart.Subtotal >= 50,
                HasItems = cart.Items.Any()
            };
        }

        /// <summary>
        /// DTO pour le résumé du panier
        /// </summary>
        public class CartSummaryDto
        {
            public int ItemCount { get; set; }
            public int ProductCount { get; set; }
            public decimal Subtotal { get; set; }
            public decimal ShippingCost { get; set; }
            public decimal Tax { get; set; }
            public decimal Total { get; set; }
            public bool IsEligibleForFreeShipping { get; set; }
            public bool HasItems { get; set; }
        }
    }
}