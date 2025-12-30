using E_commerce.Models.DTOs;

namespace E_commerce.Models.ViewModels
{
    public class ProductDetailsViewModel
    {
        public ProductDto Product { get; set; } = new();
        public int Quantity { get; set; } = 1;
    }
}
