using E_commerce.Services;
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
                .ToListAsync(); // 🚫 PAS DE Include()

            
            foreach (var product in products)
            {
                var imageUrl = await _imageService.GetImageAsync(product.Name);
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    product.ImageUrl = imageUrl;
                    await _context.SaveChangesAsync();
                }

                await Task.Delay(200); // <-- pause de 200ms entre chaque requête
            }


            await _context.SaveChangesAsync();
            Console.WriteLine("✅ Images produits mises à jour");

        }
    }

}
