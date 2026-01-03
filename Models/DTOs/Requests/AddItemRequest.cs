namespace E_commerce.Models.DTOs.Requests
{
    public class AddItemRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }
}
