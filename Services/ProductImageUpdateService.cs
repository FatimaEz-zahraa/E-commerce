using Microsoft.EntityFrameworkCore;
using E_commerce.Data;

namespace E_commerce.Services
{
    public class ProductImageUpdateService
    {
        private readonly AppDbContext _context;
        private readonly ImageService _imageService;

        public ProductImageUpdateService(
            AppDbContext context,
            ImageService imageService)
        {
            _context = context;
            _imageService = imageService;
        }

        public async Task UpdateAllImagesAsync()
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .ToListAsync();

            foreach (var product in products)
            {
                // ✅ Vérifier si l'URL est invalide
                if (IsValidHttpsImage(product.ImageUrl))
                    continue; // image déjà correcte → on passe au suivant

                // 🔍 Recherche d’une nouvelle image
                var imageUrl = await _imageService.GetProductImageUrlAsync(
                    product.Name,
                    product.Category,
                    product.Brand
                );

                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    product.ImageUrl = imageUrl;
                    Console.WriteLine($"🖼️ Image mise à jour : {product.Name}");
                }

                // ⏱️ éviter le rate limit Pexels
                await Task.Delay(200);
            }

            await _context.SaveChangesAsync();
            Console.WriteLine("✅ Mise à jour des images terminée");
        }

        /// <summary>
        /// Vérifie si l'URL est une image HTTPS valide
        /// </summary>
        private static bool IsValidHttpsImage(string? imageUrl)
        {
            return !string.IsNullOrWhiteSpace(imageUrl)
                && imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }
    }
}
