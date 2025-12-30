using E_commerce.Data;
using E_commerce.Models.DTOs;
using E_commerce.Models.Entities;
using E_commerce.Services.Interfaces;
using E_commerce.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Services.Implementations
{
    public class ReviewService : IReviewService
    {
        private readonly AppDbContext _context;

        public ReviewService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ReviewDto>> GetReviewsByProductAsync(Guid productId)
        {
            return await _context.Reviews
                .Where(r => r.ProductId == productId)
                .Select(r => new ReviewDto
                {
                    Id = r.Id,
                    ProductId = r.ProductId,
                    UserId = r.UserId,
                    Rating = r.Rating,
                    Comment = r.Comment
                })
                .ToListAsync();
        }

        public async Task<ReviewDto> AddReviewAsync(ReviewDto reviewDto)
        {
            var review = new Review
            {
                ProductId = reviewDto.ProductId,
                UserId = reviewDto.UserId,
                Rating = reviewDto.Rating,
                Comment = reviewDto.Comment
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            reviewDto.Id = review.Id;
            return reviewDto;
        }
    }

}
