using E_commerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace E_commerce.Areas.Admin.Pages
{
    [Authorize(Roles = "Admin")]
    public class SeedDataModel : PageModel
    {
        private readonly IDataSeederService _dataSeederService;
        private readonly ILogger<SeedDataModel> _logger;

        [BindProperty]
        public int UserCount { get; set; } = 50;

        [BindProperty]
        public int ProductsPerCategory { get; set; } = 25;

        [BindProperty]
        public int MaxReviewsPerProduct { get; set; } = 15;

        public string Message { get; set; }
        public bool IsSuccess { get; set; }

        public SeedDataModel(
            IDataSeederService dataSeederService,
            ILogger<SeedDataModel> logger)
        {
            _dataSeederService = dataSeederService;
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                await _dataSeederService.SeedAsync(true, UserCount, ProductsPerCategory, MaxReviewsPerProduct);

                Message = $"Données générées avec succès: {UserCount} utilisateurs, {ProductsPerCategory} produits/catégorie";
                IsSuccess = true;
            }
            catch (Exception ex)
            {
                Message = $"Erreur: {ex.Message}";
                IsSuccess = false;
                _logger.LogError(ex, "Erreur lors du peuplement");
            }

            return Page();
        }
    }
}