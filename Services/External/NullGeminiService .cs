// E_commerce/Services/External/NullGeminiService.cs
namespace E_commerce.Services.External
{
    public class NullGeminiService : IGeminiService
    {
        public Task<string> GenerateProductDescriptionAsync(string category, string productName)
        {
            return Task.FromResult($"Produit {productName} de haute qualité - {category}");
        }

        public Task<string> GenerateReviewAsync(string productName)
        {
            var reviews = new[]
            {
                "Excellent produit, je recommande!",
                "Très satisfait de mon achat",
                "Produit conforme à mes attentes",
                "Bonne qualité pour le prix",
                "Fonctionne très bien"
            };

            return Task.FromResult(reviews[new Random().Next(reviews.Length)]);
        }
    }
}