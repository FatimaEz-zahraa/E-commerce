using System.Text.Json;
using E_commerce.Models.DTOs;
using E_commerce.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace E_commerce.Services.Cache
{
    public class CachedProductService : IProductService
    {
        private readonly IProductService _inner;
        private readonly IDistributedCache _cache;

        private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan LongCacheDuration = TimeSpan.FromHours(1);

        public CachedProductService(
            IProductService inner,
            IDistributedCache cache)
        {
            _inner = inner;
            _cache = cache;
        }

        // =========================
        // CACHE KEYS
        // =========================
        private string GetAllProductsKey() => "products:all";
        private string GetProductKey(Guid id) => $"products:{id}";
        private string GetCategoriesKey() => "products:categories";
        private string GetBrandsKey() => "products:brands";

        private string GetPaginatedKey(
            int pageIndex, int pageSize,
            string? category, string? brand,
            decimal? minPrice, decimal? maxPrice,
            string? searchTerm)
        {
            return $"products:page:{pageIndex}:{pageSize}:{category}:{brand}:{minPrice}:{maxPrice}:{searchTerm}";
        }

        // =========================
        // SERIALIZATION HELPERS
        // =========================
        private async Task SetAsync<T>(string key, T data, TimeSpan? ttl = null)
        {
            var json = JsonSerializer.Serialize(data);

            await _cache.SetStringAsync(key, json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl ?? DefaultCacheDuration
                });
        }

        private async Task<T?> GetAsync<T>(string key)
        {
            var json = await _cache.GetStringAsync(key);
            return json == null ? default : JsonSerializer.Deserialize<T>(json);
        }

        // =========================
        // READ METHODS
        // =========================
        public async Task<List<ProductDto>> GetAllAsync()
        {
            var key = GetAllProductsKey();
            var cached = await GetAsync<List<ProductDto>>(key);
            if (cached != null) return cached;

            var products = await _inner.GetAllAsync();
            await SetAsync(key, products);
            return products;
        }

        public async Task<ProductDto?> GetByIdAsync(Guid id)
        {
            var key = GetProductKey(id);
            var cached = await GetAsync<ProductDto>(key);
            if (cached != null) return cached;

            var product = await _inner.GetByIdAsync(id);
            if (product != null)
                await SetAsync(key, product);

            return product;
        }

        public async Task<ProductDto?> GetProductByIdAsync(Guid id)
            => await GetByIdAsync(id);

        public async Task<PaginatedList<ProductDto>> GetPaginatedProductsAsync(
            int pageIndex = 1,
            int pageSize = 12,
            string? category = null,
            string? brand = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? searchTerm = null)
        {
            var key = GetPaginatedKey(pageIndex, pageSize, category, brand, minPrice, maxPrice, searchTerm);

            var cached = await GetAsync<PaginatedList<ProductDto>>(key);
            if (cached != null) return cached;

            var products = await _inner.GetPaginatedProductsAsync(
                pageIndex, pageSize, category, brand, minPrice, maxPrice, searchTerm);

            await SetAsync(key, products);
            return products;
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            var key = GetCategoriesKey();
            var cached = await GetAsync<List<string>>(key);
            if (cached != null) return cached;

            var categories = await _inner.GetCategoriesAsync();
            await SetAsync(key, categories, LongCacheDuration);
            return categories;
        }

        public async Task<List<string>> GetBrandsAsync()
        {
            var key = GetBrandsKey();
            var cached = await GetAsync<List<string>>(key);
            if (cached != null) return cached;

            var brands = await _inner.GetBrandsAsync();
            await SetAsync(key, brands, LongCacheDuration);
            return brands;
        }

        public async Task<List<ProductDto>> GetRelatedProductsAsync(Guid productId, int count = 4)
        {
            var key = $"products:related:{productId}:{count}";
            var cached = await GetAsync<List<ProductDto>>(key);
            if (cached != null) return cached;

            var related = await _inner.GetRelatedProductsAsync(productId, count);
            if (related.Any())
                await SetAsync(key, related);

            return related;
        }

        // =========================
        // WRITE METHODS (INVALIDATE)
        // =========================
        public async Task<ProductDto> CreateAsync(ProductDto productDto)
        {
            var product = await _inner.CreateAsync(productDto);
            await InvalidateAsync(product.Id);
            return product;
        }

        public async Task<ProductDto> UpdateAsync(ProductDto productDto)
        {
            var product = await _inner.UpdateAsync(productDto);
            await InvalidateAsync(product.Id);
            return product;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var result = await _inner.DeleteAsync(id);
            if (result)
                await InvalidateAsync(id);
            return result;
        }

        // =========================
        // CACHE INVALIDATION
        // =========================
        private async Task InvalidateAsync(Guid productId)
        {
            await _cache.RemoveAsync(GetAllProductsKey());
            await _cache.RemoveAsync(GetCategoriesKey());
            await _cache.RemoveAsync(GetBrandsKey());
            await _cache.RemoveAsync(GetProductKey(productId));
        }
    }
}
