namespace E_commerce.Models.AI
{
    public class UserIntent
    {
        public string Intent { get; set; } = "browse";
        public string? Category { get; set; }
        public decimal? BudgetMax { get; set; }
        public string? Brand { get; set; }
        public string? Priority { get; set; }
    }
}
