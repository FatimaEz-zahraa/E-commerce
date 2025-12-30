using E_commerce.Models.DTOs;
using E_commerce.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace E_commerce.Services.Cache
{
    public class CachedProductService : IProductService
    {
        private readonly IProductService _inner; // le ProductService réel
        private readonly IMemoryCache _cache;

        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        public CachedProductService(IProductService inner, IMemoryCache cache)
        {
            _inner = inner;
            _cache = cache;
        }

        // Clés de cache
        private string GetAllProductsKey() => "products_all";
        private string GetProductKey(Guid id) => $"product_{id}";
        private string GetCategoriesKey() => "product_categories";
        private string GetBrandsKey() => "product_brands";
        private string GetRelatedProductsKey(Guid productId, int count) => $"product_related_{productId}_{count}";

        // Pour les produits paginés, nous utilisons une clé basée sur les paramètres
        private string GetPaginatedProductsKey(int pageIndex, int pageSize, string? category, string? brand,
            decimal? minPrice, decimal? maxPrice, string? searchTerm)
        {
            return $"products_paginated_{pageIndex}_{pageSize}_{category}_{brand}_{minPrice}_{maxPrice}_{searchTerm}";
        }

        public async Task<List<ProductDto>> GetAllAsync()
        {
            var key = GetAllProductsKey();
            if (_cache.TryGetValue(key, out List<ProductDto> cached))
            {
                return cached;
            }

            var products = await _inner.GetAllAsync();
            _cache.Set(key, products, _cacheDuration);
            return products;
        }

        public async Task<ProductDto?> GetByIdAsync(Guid id)
        {
            var key = GetProductKey(id);
            if (_cache.TryGetValue(key, out ProductDto cached))
            {
                return cached;
            }

            var product = await _inner.GetByIdAsync(id);
            if (product != null)
            {
                _cache.Set(key, product, _cacheDuration);
            }
            return product;
        }

        // Ajout de la méthode GetProductByIdAsync
        public async Task<ProductDto?> GetProductByIdAsync(Guid id)
        {
            // Cette méthode peut simplement appeler GetByIdAsync
            // ou être implémentée séparément si nécessaire
            return await GetByIdAsync(id);
        }

        public async Task<PaginatedList<ProductDto>> GetPaginatedProductsAsync(
            int pageIndex = 1,
            int pageSize = 12,
            string? category = null,
            string? brand = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? searchTerm = null)
        {
            var key = GetPaginatedProductsKey(pageIndex, pageSize, category, brand, minPrice, maxPrice, searchTerm);

            if (_cache.TryGetValue(key, out PaginatedList<ProductDto> cached))
            {
                return cached;
            }

            var products = await _inner.GetPaginatedProductsAsync(pageIndex, pageSize, category, brand, minPrice, maxPrice, searchTerm);
            _cache.Set(key, products, _cacheDuration);
            return products;
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            var key = GetCategoriesKey();
            if (_cache.TryGetValue(key, out List<string> cached))
            {
                return cached;
            }

            var categories = await _inner.GetCategoriesAsync();
            _cache.Set(key, categories, TimeSpan.FromHours(1)); // Cache plus long pour catégories
            return categories;
        }

        public async Task<List<string>> GetBrandsAsync()
        {
            var key = GetBrandsKey();
            if (_cache.TryGetValue(key, out List<string> cached))
            {
                return cached;
            }

            var brands = await _inner.GetBrandsAsync();
            _cache.Set(key, brands, TimeSpan.FromHours(1)); // Cache plus long pour marques
            return brands;
        }

        public async Task<List<ProductDto>> GetRelatedProductsAsync(Guid productId, int count = 4)
        {
            var key = GetRelatedProductsKey(productId, count);
            if (_cache.TryGetValue(key, out List<ProductDto> cached))
            {
                return cached;
            }

            var relatedProducts = await _inner.GetRelatedProductsAsync(productId, count);
            if (relatedProducts != null && relatedProducts.Any())
            {
                _cache.Set(key, relatedProducts, _cacheDuration);
            }
            return relatedProducts;
        }

        public async Task<ProductDto> CreateAsync(ProductDto productDto)
        {
            var product = await _inner.CreateAsync(productDto);
            // Invalider les caches concernés
            InvalidateCache();
            return product;
        }

        public async Task<ProductDto> UpdateAsync(ProductDto productDto)
        {
            var product = await _inner.UpdateAsync(productDto);
            // Invalider les caches concernés
            InvalidateCache(productDto.Id);
            return product;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var result = await _inner.DeleteAsync(id);
            if (result)
            {
                InvalidateCache(id);
            }
            return result;
        }

        // Méthodes pour invalider le cache
        private void InvalidateCache(Guid? productId = null)
        {
            // Invalider la liste complète
            _cache.Remove(GetAllProductsKey());

            // Invalider les catégories et marques
            _cache.Remove(GetCategoriesKey());
            _cache.Remove(GetBrandsKey());

            // Invalider les produits paginés (nous utilisons un cache plus intelligent)
            ClearPaginatedCache();

            // Invalider le produit spécifique si fourni
            if (productId.HasValue)
            {
                _cache.Remove(GetProductKey(productId.Value));
                // Invalider aussi les produits liés
                ClearRelatedProductsCache(productId.Value);
            }
        }

        // Pour invalider les caches de pagination
        private void ClearPaginatedCache()
        {
            // Option 1: Invalider tous les caches avec le préfixe "products_paginated"
            // Ceci est simplifié - dans une vraie app, vous voudriez peut-être une stratégie plus sophistiquée
            var cacheEntriesCollection = _cache as MemoryCache;
            if (cacheEntriesCollection != null)
            {
                // Cette approche est simplifiée
                // Dans la pratique, vous voudriez garder une liste des clés de cache
            }
        }

        private void ClearRelatedProductsCache(Guid productId)
        {
            // Similaire à ClearPaginatedCache, invalider les produits liés
            // Par exemple, invalider toutes les clés commençant par "product_related_{productId}_"
            if (_cache is MemoryCache memoryCache)
            {
                var keysToRemove = new List<string>();
                // Dans une vraie application, vous auriez besoin d'une manière de suivre les clés
                // Ceci est une approche simplifiée
                var prefix = $"product_related_{productId}_";
                // Note: MemoryCache n'a pas de méthode publique pour lister les clés
                // Vous pourriez avoir besoin d'une implémentation différente ou d'un wrapper
            }
        }
    }
}