using E_commerce.Data;
using E_commerce.Helpers;
using E_commerce.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Pages.Cart
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public CartDto Cart { get; set; } = new();  // Panier côté cookie
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        // =========================
        // AFFICHAGE DU PANIER
        // =========================
        public async Task OnGetAsync()
        {
            var cookieCart = CookieHelper.GetOrCreateCart(HttpContext);

            Cart = new CartDto
            {
                Items = cookieCart.Items.Select(c => new CartItemDto
                {
                    ProductId = c.ProductId,
                    ProductName = c.ProductName,
                    Price = c.Price,
                    Quantity = c.Quantity,
                    ImageUrl = c.ImageUrl,
                    Brand = c.Brand,
                    Category = c.Category,
                    MaxQuantity = int.MaxValue
                }).ToList()
            };

            Cart.CalculateTotals();
        }

        // =========================
        // AJOUT D’UN PRODUIT
        // =========================
        public async Task<IActionResult> OnPostAddItem(Guid productId, int quantity = 1)
        {
            try
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);

                if (product == null)
                {
                    ErrorMessage = "Produit non trouvé";
                    return Page();
                }

                if (product.StockQuantity <= 0)
                {
                    ErrorMessage = "Produit en rupture de stock";
                    return Page();
                }

                quantity = Math.Min(quantity, product.StockQuantity);

                CookieHelper.AddProductToCart(
                    HttpContext,
                    productId,
                    product.Name,
                    product.Price,
                    quantity,
                    product.ImageUrl,
                    product.Brand,
                    product.Category
                );

                SuccessMessage = $"{product.Name} ajouté au panier !";

                // Debug du cookie
                Console.WriteLine(CookieHelper.DebugCart(HttpContext));

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erreur: {ex.Message}";
                return Page();
            }
        }

        // =========================
        // MISE À JOUR QUANTITÉ
        // =========================
        public IActionResult OnPostUpdateQuantity(Guid productId, int quantity)
        {
            CookieHelper.UpdateCartQuantity(HttpContext, productId, quantity);
            return RedirectToPage();
        }

        // =========================
        // SUPPRESSION D’UN PRODUIT
        // =========================
        public IActionResult OnPostRemove(Guid productId)
        {
            CookieHelper.RemoveFromCart(HttpContext, productId);
            return RedirectToPage();
        }

        // =========================
        // VIDER LE PANIER
        // =========================
        public IActionResult OnPostClear()
        {
            CookieHelper.ClearCart(HttpContext);
            return RedirectToPage();
        }

        // =========================
        // COMPTEUR DU PANIER (AJAX)
        // =========================
        public IActionResult OnGetCartCount()
        {
            var count = CookieHelper.GetCartItemCount(HttpContext);
            return new JsonResult(new { count });
        }
    }
}
