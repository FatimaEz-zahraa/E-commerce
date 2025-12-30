using E_commerce.Models.DTOs;

namespace E_commerce.Services.Interfaces
{
    public interface IRecommendationService
    {
        Task<List<ProductDto>> GetRecommendationsAsync(string userId);
    }

}
