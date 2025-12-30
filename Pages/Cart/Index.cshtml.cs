// Pages/Cart/Index.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using E_commerce.Services.Interfaces;
using E_commerce.Models.DTOs;

namespace E_commerce.Pages.Cart
{
    public class IndexModel : PageModel
    {
        private readonly IHttpCartService _cartService;
        private readonly IProductService _productService;

        public CartDto Cart { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public IndexModel(IHttpCartService cartService, IProductService productService)
        {
            _cartService = cartService;
            _productService = productService;
        }

        public async Task OnGetAsync()
        {
            Cart = await _cartService.GetCartAsync();
        }

        public async Task<IActionResult> OnPostAddItemAsync(Guid productId)
        {
            try
            {
                var product = await _productService.GetByIdAsync(productId);
                if (product == null)
                {
                    ErrorMessage = "Produit non trouvé";
                    return Page();
                }

                await _cartService.AddItemAsync(productId, 1);

                SuccessMessage = $"{product.Name} ajouté au panier";
                Cart = await _cartService.GetCartAsync();
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erreur: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdateQuantityAsync(Guid productId, int quantity)
        {
            try
            {
                if (quantity < 0) quantity = 0;

                await _cartService.UpdateQuantityAsync(productId, quantity);
                Cart = await _cartService.GetCartAsync();

                if (quantity == 0)
                {
                    SuccessMessage = "Produit retiré du panier";
                }
                else
                {
                    SuccessMessage = "Quantité mise à jour";
                }

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erreur: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostRemoveAsync(Guid productId)
        {
            try
            {
                await _cartService.RemoveItemAsync(productId);
                Cart = await _cartService.GetCartAsync();
                SuccessMessage = "Produit retiré du panier";
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erreur: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostClearAsync()
        {
            try
            {
                await _cartService.ClearCartAsync();
                Cart = await _cartService.GetCartAsync();
                SuccessMessage = "Panier vidé";
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erreur: {ex.Message}";
                return Page();
            }
        }

        // API endpoint pour AJAX
        public async Task<JsonResult> OnGetCartCountAsync()
        {
            var count = await _cartService.GetItemCountAsync();
            return new JsonResult(new { count });
        }

        public async Task<JsonResult> OnGetCartTotalAsync()
        {
            var total = await _cartService.GetTotalAsync();
            return new JsonResult(new { total });
        }

        // Nouvelle méthode pour AJAX
        public async Task<JsonResult> OnGetCartDetailsAsync()
        {
            var cart = await _cartService.GetCartAsync();
            return new JsonResult(new
            {
                items = cart.Items.Select(i => new
                {
                    i.ProductId,
                    i.ProductName,
                    i.Price,
                    i.Quantity,
                    i.TotalPrice
                }),
                subtotal = cart.Subtotal,
                shipping = cart.ShippingCost,
                tax = cart.Tax,
                total = cart.Total,
                itemCount = cart.TotalItems
            });
        }
    }
}