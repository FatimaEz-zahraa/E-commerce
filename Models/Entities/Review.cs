using System.ComponentModel.DataAnnotations;

namespace E_commerce.Models.Entities
{
    public class Review
    {
        public Guid Id { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsVerifiedPurchase { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Relations
        public virtual Product? Product { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
}