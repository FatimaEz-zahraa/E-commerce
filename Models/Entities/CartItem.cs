using System.ComponentModel.DataAnnotations;

namespace E_commerce.Models.Entities
{
    public class CartItem
    {
        public Guid Id { get; set; }

        [Required]
        public Guid CartId { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Relations
        public virtual Cart? Cart { get; set; }
        public virtual Product? Product { get; set; }
    }

}
