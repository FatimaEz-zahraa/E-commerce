using E_commerce.Models.DTOs;

namespace E_commerce.Services.Interfaces
{
    public interface IReviewService
    {
        Task<List<ReviewDto>> GetReviewsByProductAsync(Guid productId);
        Task<ReviewDto> AddReviewAsync(ReviewDto reviewDto);
    }
}

