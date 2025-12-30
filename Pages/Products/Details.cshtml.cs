using E_commerce.Helpers;
using E_commerce.Models.DTOs;
using E_commerce.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace E_commerce.Pages.Products
{
    [AllowAnonymous]
    public class DetailsModel : PageModel
    {
        private readonly IProductService _productService;
        private readonly ICartService _cartService;

        public ProductDto? Product { get; set; }

        [BindProperty]
        public int Quantity { get; set; } = 1;

        public DetailsModel(IProductService productService, ICartService cartService)
        {
            _productService = productService;
            _cartService = cartService;
        }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            Product = await _productService.GetByIdAsync(id);

            if (Product == null)
            {
                return RedirectToPage("/Products/Index");
            }

            return Page();
        }

        [AllowAnonymous]
        public async Task<IActionResult> OnPostAddToCartAsync(Guid id)
        {
            try
            {
                // Vérifier que le produit existe
                Product = await _productService.GetByIdAsync(id);
                if (Product == null)
                {
                    TempData["ErrorMessage"] = "Le produit n'existe pas ou n'est plus disponible.";
                    return RedirectToPage();
                }

                var userId = User.Identity?.IsAuthenticated == true
                    ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    : null;

                var cartId = CookieHelper.GetOrCreateCartId(HttpContext);

                await _cartService.AddToCartAsync(userId, cartId, id, Quantity);

                TempData["SuccessMessage"] = $"{Product.Name} ajouté au panier!";
                return RedirectToPage("/Cart/Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Erreur lors de l'ajout au panier.";
                return RedirectToPage();
            }
        }
    }
}
