// Dans Product.cs
using E_commerce.Models.Entities;

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }

    // Ajoutez ces propriétés
    public string Category { get; set; } = "";
    public string Brand { get; set; } = "";
    public string? ImageUrl { get; set; }  // ← NOUVEAU
    public decimal Rating { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}