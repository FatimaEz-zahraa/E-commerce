// Dans Index.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using E_commerce.Services.Interfaces;
using E_commerce.Models.DTOs;
using E_commerce.Helpers;
using E_commerce.Data;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Pages.Products
{
    public class IndexModel : PageModel
    {
        private readonly IProductService _productService;
        private readonly AppDbContext _context;

        public IndexModel(IProductService productService, AppDbContext context)
        {
            _productService = productService;
            _context = context;
        }

        public PaginatedList<ProductDto> Products { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Category { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Brand { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal? MinPrice { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal? MaxPrice { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SortBy { get; set; } = "newest";

        [BindProperty(SupportsGet = true)]
        public int? MinRating { get; set; }

        public int TotalProducts => Products.TotalCount;
        public List<string> Categories { get; set; } = new();
        public List<string> Brands { get; set; } = new();

        // =========================
        // Récupération des produits
        // =========================
        public async Task OnGetAsync(int pageIndex = 1, int pageSize = 12)
        {
            Products = await _productService.GetPaginatedProductsAsync(
                pageIndex: pageIndex,
                pageSize: pageSize,
                category: Category,
                brand: Brand,
                minPrice: MinPrice,
                maxPrice: MaxPrice,
                searchTerm: SearchTerm
            );

            ApplySorting(Products.Items);

            Categories = await _productService.GetCategoriesAsync();
            Brands = await _productService.GetBrandsAsync();
        }

        private void ApplySorting(List<ProductDto> products)
        {
            switch (SortBy)
            {
                case "price_asc": products.Sort((a, b) => a.Price.CompareTo(b.Price)); break;
                case "price_desc": products.Sort((a, b) => b.Price.CompareTo(a.Price)); break;
                case "rating": products.Sort((a, b) => b.Rating.CompareTo(a.Rating)); break;
                case "popular": products.Sort((a, b) => b.ReviewCount.CompareTo(a.ReviewCount)); break;
                default: products.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt)); break;
            }
        }

        // =========================
        // Ajouter un produit au panier côté cookie
        // =========================
        public async Task<IActionResult> OnPostAddToCartAsync(Guid productId, int quantity = 1)
        {
            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
                if (product == null || product.StockQuantity <= 0)
                {
                    TempData["ErrorMessage"] = "Produit indisponible";
                    return RedirectToPage();
                }

                quantity = Math.Min(quantity, product.StockQuantity);

                CookieHelper.AddProductToCart(
                    HttpContext,
                    product.Id,
                    product.Name,
                    product.Price,
                    quantity,
                    product.ImageUrl,
                    product.Brand,
                    product.Category
                );

                TempData["SuccessMessage"] = $"{product.Name} ajouté au panier !";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Erreur: {ex.Message}";
                return RedirectToPage();
            }
        }

        public int GetCategoryCount(string category)
        {
            // Méthode pour compter les produits par catégorie dans la liste actuelle
            return Products.Items.Count(p => p.Category == category);
        }

        public int GetBrandCount(string brand)
        {
            return Products.Items.Count(p => p.Brand == brand);
        }
    }
}