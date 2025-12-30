using E_commerce.Models.DTOs;

namespace E_commerce.Models.ViewModels
{
    public class CartViewModel
    {
        public CartDto Cart { get; set; } = new();
    }

}