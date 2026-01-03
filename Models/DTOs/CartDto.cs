using System;
using System.Collections.Generic;
using System.Linq;

namespace E_commerce.Models.DTOs
{
    public class CartDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string UserId { get; set; } = string.Empty;
        public List<CartItemDto> Items { get; set; } = new List<CartItemDto>();

        // CHANGER ces propriétés pour avoir des setters
        public decimal Subtotal { get; set; }
        public decimal ShippingCost { get; set; } = 0;
        public decimal Tax { get; set; } = 0;
        public decimal Total { get; set; }
        public int TotalItems { get; set; }

        // Anciennes propriétés calculées (garder pour compatibilité si nécessaire)
        // public decimal Subtotal => Items.Sum(i => i.TotalPrice);
        // public decimal Total => Subtotal + ShippingCost + Tax;
        // public int TotalItems => Items.Sum(i => i.Quantity);

        // =========================
        // Méthodes utiles
        // =========================

        public void AddItem(CartItemDto item)
        {
            var existing = Items.FirstOrDefault(i => i.ProductId == item.ProductId);
            if (existing != null)
            {
                existing.Quantity += item.Quantity;
            }
            else
            {
                Items.Add(item);
            }

            // Recalculer les totaux
            CalculateTotals();
        }

        public void UpdateQuantity(Guid productId, int quantity)
        {
            var existing = Items.FirstOrDefault(i => i.ProductId == productId);
            if (existing != null)
            {
                if (quantity <= 0)
                    Items.Remove(existing);
                else
                    existing.Quantity = quantity;

                CalculateTotals();
            }
        }

        public void RemoveItem(Guid productId)
        {
            var existing = Items.FirstOrDefault(i => i.ProductId == productId);
            if (existing != null)
            {
                Items.Remove(existing);
                CalculateTotals();
            }
        }

        public void Clear()
        {
            Items.Clear();
            CalculateTotals();
        }

        // Méthode pour recalculer tous les totaux
        public void CalculateTotals()
        {
            TotalItems = Items.Sum(i => i.Quantity);
            Subtotal = Items.Sum(i => i.TotalPrice);

            // Livraison gratuite au-dessus de 50€
            ShippingCost = Subtotal > 50 ? 0 : 4.99m;

            // TVA 20%
            Tax = Subtotal * 0.20m;
            Total = Subtotal + ShippingCost + Tax;
        }
    }

    public class CartItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal TotalPrice => Price * Quantity;
        public string? ImageUrl { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public bool IsAvailable { get; set; } = true;
        public int MaxQuantity { get; set; } = 10;
    }
}