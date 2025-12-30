// Helpers/CartHelper.cs
using E_commerce.Services.Interfaces;

namespace E_commerce.Helpers
{
    public static class CartHelper
    {
        private const string CartIdCookieName = "CartSessionId";

        /// <summary>
        /// Obtient ou crée un ID de panier de session
        /// </summary>
        public static string GetOrCreateCartId(HttpContext context)
        {
            var cartId = context.Request.Cookies[CartIdCookieName];

            if (string.IsNullOrEmpty(cartId))
            {
                cartId = Guid.NewGuid().ToString();
                SetCartIdCookie(context, cartId);
            }

            return cartId;
        }

        private static void SetCartIdCookie(HttpContext context, string cartId)
        {
            context.Response.Cookies.Append(CartIdCookieName, cartId, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(30),
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(30)
            });
        }

        public static void ClearCartId(HttpContext context)
        {
            context.Response.Cookies.Delete(CartIdCookieName);
        }

        /// <summary>
        /// Obtient l'ID de l'utilisateur connecté ou null si anonyme
        /// </summary>
        public static string? GetUserId(HttpContext context)
        {
            return context.User.Identity?.IsAuthenticated == true
                ? context.User.Identity.Name
                : null;
        }

        /// <summary>
        /// Obtient l'ID utilisateur depuis les claims (plus fiable)
        /// </summary>
        public static string? GetUserIdFromClaims(HttpContext context)
        {
            return context.User.Identity?.IsAuthenticated == true
                ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null;
        }

        /// <summary>
        /// Obtient les identifiants complets du panier (userId et cartId)
        /// </summary>
        public static (string? userId, string cartId) GetCartIdentifiers(HttpContext context)
        {
            var userId = GetUserIdFromClaims(context);
            var cartId = GetOrCreateCartId(context);
            return (userId, cartId);
        }

        /// <summary>
        /// Vérifie si l'utilisateur est connecté
        /// </summary>
        public static bool IsUserAuthenticated(HttpContext context)
        {
            return context.User.Identity?.IsAuthenticated == true;
        }

        /// <summary>
        /// Obtient le nom d'affichage de l'utilisateur
        /// </summary>
        public static string? GetUserDisplayName(HttpContext context)
        {
            if (!IsUserAuthenticated(context))
                return null;

            return context.User.Identity?.Name ??
                   context.User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value ??
                   context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        }

        /// <summary>
        /// Migre le panier lors de la connexion
        /// </summary>
        public static async Task<bool> MigrateCartOnLoginAsync(
            HttpContext context,
            ICartService cartService,
            string username)
        {
            try
            {
                var cartId = GetOrCreateCartId(context);
                await cartService.MergeCartAsync(username, cartId);
                ClearCartId(context);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Formate le montant pour l'affichage
        /// </summary>
        public static string FormatAmount(decimal amount)
        {
            return amount.ToString("C", System.Globalization.CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Obtient l'icône du panier selon le nombre d'articles
        /// </summary>
        public static string GetCartIcon(int itemCount)
        {
            return itemCount > 0 ? "🛒" : "📦";
        }

        /// <summary>
        /// Obtient la classe CSS pour le badge du panier
        /// </summary>
        public static string GetBadgeClass(int itemCount)
        {
            if (itemCount <= 0) return "d-none";
            if (itemCount > 9) return "badge rounded-pill bg-danger";
            return "badge rounded-pill bg-primary";
        }

        /// <summary>
        /// Obtient le texte du badge (avec limite à 9+)
        /// </summary>
        public static string GetBadgeText(int itemCount)
        {
            if (itemCount <= 0) return "";
            if (itemCount > 9) return "9+";
            return itemCount.ToString();
        }

        /// <summary>
        /// Génère un message de résumé du panier
        /// </summary>
        public static string GetCartSummaryMessage(int itemCount, decimal total)
        {
            if (itemCount <= 0)
                return "Votre panier est vide";

            var itemText = itemCount == 1 ? "article" : "articles";
            return $"{itemCount} {itemText} - {FormatAmount(total)}";
        }

        /// <summary>
        /// Obtient l'URL du panier avec paramètres
        /// </summary>
        public static string GetCartUrl()
        {
            return "/Cart/Index";
        }

        /// <summary>
        /// Obtient l'URL de la page de checkout
        /// </summary>
        public static string GetCheckoutUrl()
        {
            return "/Checkout/Index";
        }

        /// <summary>
        /// Vérifie si le panier est éligible à la livraison gratuite
        /// </summary>
        public static bool IsEligibleForFreeShipping(decimal subtotal)
        {
            return subtotal >= 50m;
        }

        /// <summary>
        /// Calcule le montant manquant pour la livraison gratuite
        /// </summary>
        public static decimal GetAmountNeededForFreeShipping(decimal subtotal)
        {
            var needed = 50m - subtotal;
            return needed > 0 ? needed : 0;
        }

        /// <summary>
        /// Obtient un message d'encouragement pour la livraison gratuite
        /// </summary>
        public static string GetFreeShippingMessage(decimal subtotal)
        {
            if (subtotal >= 50m)
                return "🎉 Félicitations ! Vous avez la livraison gratuite !";

            var needed = GetAmountNeededForFreeShipping(subtotal);
            return $"Ajoutez encore {FormatAmount(needed)} pour bénéficier de la livraison gratuite !";
        }
    }
}