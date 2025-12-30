// Dans ProductDto.cs
public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string Category { get; set; } = "";
    public string Brand { get; set; } = "";
    public string? ImageUrl { get; set; }
    public decimal Rating { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }

    // Propriétés calculées ou supplémentaires
    public int ReviewCount { get; set; }
    public decimal? OldPrice { get; set; } // Pour les promotions
    public int? DiscountPercentage { get; set; } // Pourcentage de réduction
}