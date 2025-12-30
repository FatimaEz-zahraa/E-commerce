using System.ComponentModel.DataAnnotations;

namespace E_commerce.Models.Entities
{
    public class WishlistItem
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public Guid ProductId { get; set; }

        public DateTime AddedDate { get; set; } = DateTime.UtcNow;
        public DateTime? RemovedDate { get; set; }

        public bool IsActive { get; set; } = true;

        // Priorité/notes utilisateur
        [Range(1, 5)]
        public int? Priority { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        // Pour le suivi des modifications
        public DateTime? LastViewed { get; set; }
        public int ViewCount { get; set; } = 0;

        // Relations
        public virtual ApplicationUser? User { get; set; }
        public virtual Product? Product { get; set; }
    }
}