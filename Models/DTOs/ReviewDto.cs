namespace E_commerce.Models.DTOs
{
    public class ReviewDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string UserId { get; set; } = "";
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }

}
