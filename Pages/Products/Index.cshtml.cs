// Dans Index.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using E_commerce.Services.Interfaces;
using E_commerce.Models.DTOs;

namespace E_commerce.Pages.Products
{
    public class IndexModel : PageModel
    {
        private readonly IProductService _productService;

        public IndexModel(IProductService productService)
        {
            _productService = productService;
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

        public async Task OnGetAsync(int pageIndex = 1, int pageSize = 12)
        {
            // Récupérer les produits paginés via le service
            Products = await _productService.GetPaginatedProductsAsync(
                pageIndex: pageIndex,
                pageSize: pageSize,
                category: Category,
                brand: Brand,
                minPrice: MinPrice,
                maxPrice: MaxPrice,
                searchTerm: SearchTerm
            );

            // Appliquer le tri manuellement si nécessaire
            if (!string.IsNullOrEmpty(SortBy))
            {
                ApplySorting(Products.Items);
            }

            // Récupérer les catégories et marques
            Categories = await _productService.GetCategoriesAsync();
            Brands = await _productService.GetBrandsAsync();

            Console.WriteLine($"DEBUG Index - Produits trouvés: {Products.Items.Count}");
            Console.WriteLine($"DEBUG Index - Rating du premier produit: {Products.Items.FirstOrDefault()?.Rating}");
        }

        private void ApplySorting(List<ProductDto> products)
        {
            switch (SortBy)
            {
                case "price_asc":
                    products = products.OrderBy(p => p.Price).ToList();
                    break;
                case "price_desc":
                    products = products.OrderByDescending(p => p.Price).ToList();
                    break;
                case "rating":
                    products = products.OrderByDescending(p => p.Rating).ToList();
                    break;
                case "popular":
                    products = products.OrderByDescending(p => p.ReviewCount).ToList();
                    break;
                default: // "newest"
                    products = products.OrderByDescending(p => p.CreatedAt).ToList();
                    break;
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