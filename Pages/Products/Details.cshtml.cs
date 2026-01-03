using E_commerce.Helpers;
using E_commerce.Models.DTOs;
using E_commerce.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace E_commerce.Pages.Products
{
    [AllowAnonymous]
    public class DetailsModel : PageModel
    {
        private readonly IProductService _productService;
        private readonly ICartService _cartService;

        public ProductDto? Product { get; set; }

        // NE PAS utiliser [BindProperty] pour quantity car ça interfère avec le formulaire
        // public int Quantity { get; set; } = 1; // ENLEVEZ CETTE LIGNE

        public DetailsModel(IProductService productService, ICartService cartService)
        {
            _productService = productService;
            _cartService = cartService;
        }

        // =========================
        // AFFICHER LE PRODUIT
        // =========================
        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            Console.WriteLine($"DEBUG OnGetAsync: id={id}");
            Product = await _productService.GetByIdAsync(id);

            if (Product == null)
            {
                Console.WriteLine($"DEBUG: Produit non trouvé: {id}");
                return RedirectToPage("/Products/Index");
            }

            Console.WriteLine($"DEBUG: Produit trouvé: {Product.Name}");
            return Page();
        }

        // =========================
        // AJOUT AU PANIER
        // =========================
        // =========================
        // AJOUT AU PANIER
        // =========================
        [AllowAnonymous]
        public async Task<IActionResult> OnPostAddToCartAsync(Guid id, int quantity)
        {
            try
            {
                var product = await _productService.GetByIdAsync(id);
                if (product == null) return NotFound();

                var userId = User.Identity?.IsAuthenticated == true
                    ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

                await _cartService.AddToCartAsync(userId, id, quantity);

                TempData["SuccessMessage"] = "Produit ajouté !";
                return RedirectToPage("/Cart/Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToPage(new { id });
            }
        }
    }
}