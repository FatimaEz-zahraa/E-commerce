// Models/Entities/Cart.cs
using System.ComponentModel.DataAnnotations;

namespace E_commerce.Models.Entities
{
    public class Cart
    {
        public Guid Id { get; set; }

        [StringLength(450)] // La même longueur que IdentityUser.Id
        public string? UserId { get; set; } // Rend nullable

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Relations
        public virtual ApplicationUser? User { get; set; }
        public virtual ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    }
}