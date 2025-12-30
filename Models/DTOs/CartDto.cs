// Models/DTOs/CartDto.cs
using System.Text.Json.Serialization;

namespace E_commerce.Models.DTOs
{
    public class CartDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        [JsonPropertyName("items")]
        public List<CartItemDto> Items { get; set; } = new();

        // Ces propriétés devraient être calculées, pas stockées
        [JsonIgnore]
        public decimal Subtotal => Items.Sum(i => i.Price * i.Quantity);

        [JsonIgnore]
        public decimal ShippingCost => Subtotal >= 50 ? 0 : 4.99m;

        [JsonIgnore]
        public decimal Tax => Math.Round(Subtotal * 0.2m, 2); // TVA 20%

        [JsonIgnore]
        public decimal Total => Subtotal + ShippingCost + Tax;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [JsonIgnore]
        public int TotalItems => Items.Sum(i => i.Quantity);

        // Méthodes utilitaires pour le cookie
        public void AddItem(CartItemDto item)
        {
            var existingItem = Items.FirstOrDefault(i => i.ProductId == item.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += item.Quantity;
                existingItem.Quantity = Math.Min(existingItem.Quantity, existingItem.MaxQuantity);
            }
            else
            {
                Items.Add(item);
            }
        }

        public void RemoveItem(Guid productId)
        {
            Items.RemoveAll(i => i.ProductId == productId);
        }

        public void UpdateQuantity(Guid productId, int quantity)
        {
            var item = Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                if (quantity <= 0)
                {
                    RemoveItem(productId);
                }
                else
                {
                    item.Quantity = Math.Min(quantity, item.MaxQuantity);
                }
            }
        }

        public void Clear()
        {
            Items.Clear();
        }
    }

    public class CartItemDto
    {
        [JsonPropertyName("productId")]
        public Guid ProductId { get; set; }

        [JsonPropertyName("productName")]
        public string ProductName { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("brand")]
        public string? Brand { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("isAvailable")]
        public bool IsAvailable { get; set; } = true;

        [JsonPropertyName("maxQuantity")]
        public int MaxQuantity { get; set; } = 10;

        [JsonIgnore]
        public decimal TotalPrice => Price * Quantity;
    }
}