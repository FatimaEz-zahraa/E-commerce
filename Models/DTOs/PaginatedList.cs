using Microsoft.EntityFrameworkCore;

namespace E_commerce.Models.DTOs
{
    public class PaginatedList<T>
    {
        public List<T> Items { get; set; } = new();
        public int PageIndex { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public int PageSize { get; set; }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
        public int StartItem => (PageIndex - 1) * PageSize + 1;
        public int EndItem => Math.Min(PageIndex * PageSize, TotalCount);

        public static async Task<PaginatedList<T>> CreateAsync(
            IQueryable<T> source,
            int pageIndex,
            int pageSize)
        {
            var count = await source.CountAsync();
            var items = await source
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedList<T>
            {
                Items = items,
                PageIndex = pageIndex,
                TotalPages = (int)Math.Ceiling(count / (double)pageSize),
                TotalCount = count,
                PageSize = pageSize
            };
        }
    }
}