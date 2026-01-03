namespace E_commerce.Models.DTOs.Requests
{
    public class UpdateQuantityRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
