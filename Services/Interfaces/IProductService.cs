using E_commerce.Models.DTOs;

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync();
    Task<ProductDto?> GetByIdAsync(Guid id);
    Task<ProductDto?> GetProductByIdAsync(Guid id); // Optionnel - peut être supprimé
    Task<ProductDto> CreateAsync(ProductDto productDto);
    Task<ProductDto> UpdateAsync(ProductDto productDto);
    Task<bool> DeleteAsync(Guid id);
    Task<PaginatedList<ProductDto>> GetPaginatedProductsAsync(
        int pageIndex = 1,
        int pageSize = 12,
        string? category = null,
        string? brand = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? searchTerm = null);
    Task<List<string>> GetCategoriesAsync();
    Task<List<string>> GetBrandsAsync();
    Task<List<ProductDto>> GetRelatedProductsAsync(Guid productId, int count = 4);
}