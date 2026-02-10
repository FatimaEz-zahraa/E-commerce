using E_commerce.Data;
using E_commerce.Models.DTOs;
using E_commerce.Models.Entities;
using E_commerce.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using AutoMapper;

namespace E_commerce.Services.Implementations;

public class ProductService : IProductService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public ProductService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<List<ProductDto>> GetAllAsync()
    {
        var products = await _context.Products
            .Include(p => p.Reviews)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return _mapper.Map<List<ProductDto>>(products);
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        var product = await _context.Products
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        if (product == null)
            return null;

        return _mapper.Map<ProductDto>(product);
    }

    // Alias pour GetByIdAsync
    public async Task<ProductDto?> GetProductByIdAsync(Guid id)
    {
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
        Console.WriteLine("=== DEBUG GetPaginatedProductsAsync ===");
        Console.WriteLine($"PageIndex: {pageIndex}, PageSize: {pageSize}");

        // MODIFIEZ ICI : Utilisez une projection pour éviter le problème de type
        var query = _context.Products
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.StockQuantity,
                p.Category,
                p.Brand,
                p.ImageUrl,
                Rating = p.Rating, // Conversion explicite ici dans la requête
                p.CreatedAt,
                p.IsActive,
                ReviewCount = p.Reviews.Count
            })
            .AsQueryable();

        Console.WriteLine($"Produits actifs de base: {await query.CountAsync()}");

        // Appliquer les filtres (maintenant sur l'objet anonyme)
        if (!string.IsNullOrEmpty(category))
        {
            Console.WriteLine($"Filtre catégorie: '{category}'");
            query = query.Where(p => p.Category == category);
        }

        if (!string.IsNullOrEmpty(brand))
        {
            Console.WriteLine($"Filtre marque: '{brand}'");
            query = query.Where(p => p.Brand == brand);
        }

        if (minPrice.HasValue)
        {
            Console.WriteLine($"Filtre prix min: {minPrice}");
            query = query.Where(p => p.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            Console.WriteLine($"Filtre prix max: {maxPrice}");
            query = query.Where(p => p.Price <= maxPrice.Value);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            Console.WriteLine($"Filtre recherche: '{searchTerm}'");
            query = query.Where(p =>
                p.Name.Contains(searchTerm) ||
                p.Description.Contains(searchTerm) ||
                p.Category.Contains(searchTerm));
        }

        // IMPORTANT: Vérifiez le compte après filtres
        var countAfterFilters = await query.CountAsync();
        Console.WriteLine($"Produits après filtres: {countAfterFilters}");

        // Trier par date de création
        query = query.OrderByDescending(p => p.CreatedAt);

        // MODIFIEZ ICI : Créez votre propre pagination au lieu d'utiliser PaginatedList<Product>
        var count = await query.CountAsync();
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Console.WriteLine($"Résultat paginé - Items: {items.Count}, Total: {count}");

        if (items.Any())
        {
            Console.WriteLine($"Premier produit: {items[0].Name}");
            Console.WriteLine($"Rating du premier produit: {items[0].Rating} (type: {items[0].Rating.GetType()})");
        }

        // Mapper manuellement vers ProductDto
        var mappedItems = items.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description ?? "",
            Price = p.Price,
            StockQuantity = p.StockQuantity,
            Category = p.Category ?? "",
            Brand = p.Brand ?? "",
            ImageUrl = p.ImageUrl,
            Rating = p.Rating, 
            CreatedAt = p.CreatedAt,
            IsActive = p.IsActive,
            ReviewCount = p.ReviewCount,
            OldPrice = p.Price > 100 ? p.Price * 1.2m : (decimal?)null,
            DiscountPercentage = p.Price > 100 ? 20 : 0
        }).ToList();

        return new PaginatedList<ProductDto>
        {
            Items = mappedItems,
            PageIndex = pageIndex,
            TotalPages = (int)Math.Ceiling(count / (double)pageSize),
            TotalCount = count,
            PageSize = pageSize
        };
    }



    public async Task<ProductDto> CreateAsync(ProductDto productDto)
    {
        var product = _mapper.Map<Product>(productDto);
        product.Id = Guid.NewGuid();
        product.CreatedAt = DateTime.UtcNow;

        if (product.Rating == 0)
            product.Rating = 4.0m; // Valeur par défaut

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> UpdateAsync(ProductDto productDto)
    {
        var product = await _context.Products.FindAsync(productDto.Id);
        if (product == null)
            throw new ArgumentException("Produit non trouvé");

        // Mettre à jour les propriétés
        product.Name = productDto.Name;
        product.Description = productDto.Description;
        product.Price = productDto.Price;
        product.StockQuantity = productDto.StockQuantity;
        product.Category = productDto.Category;
        product.Brand = productDto.Brand;
        product.ImageUrl = productDto.ImageUrl;
        product.Rating = productDto.Rating;
        product.IsActive = productDto.IsActive;

        await _context.SaveChangesAsync();

        return _mapper.Map<ProductDto>(product);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
            return false;

        // Soft delete
        product.IsActive = false;
        await _context.SaveChangesAsync();

        return true;
    }




    public async Task<List<string>> GetCategoriesAsync()
    {
        Console.WriteLine("=== DEBUG GetCategoriesAsync ===");

        var categories = await _context.Products
            .Where(p => p.IsActive)
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        Console.WriteLine($"Catégories trouvées: {categories.Count}");
        if (categories.Any())
        {
            Console.WriteLine($"Exemples: {string.Join(", ", categories.Take(3))}");
        }

        return categories;
    }

    public async Task<List<string>> GetBrandsAsync()
    {
        Console.WriteLine("=== DEBUG GetBrandsAsync ===");

        var brands = await _context.Products
            .Where(p => p.IsActive)
            .Select(p => p.Brand)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync();

        Console.WriteLine($"Marques trouvées: {brands.Count}");
        if (brands.Any())
        {
            Console.WriteLine($"Exemples: {string.Join(", ", brands.Take(3))}");
        }

        return brands;
    }

    public async Task<List<ProductDto>> GetRelatedProductsAsync(Guid productId, int count = 4)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null)
            return new List<ProductDto>();

        var related = await _context.Products
            .Where(p => p.IsActive &&
                       p.Category == product.Category &&
                       p.Id != productId)
            .OrderByDescending(p => p.Rating)
            .Take(count)
            .ToListAsync();

        return _mapper.Map<List<ProductDto>>(related);
    }

    // Ajoutez ce paramètre à GetPaginatedProductsAsync dans votre service
    public async Task<PaginatedList<ProductDto>> GetPaginatedProductsAsync(
        int pageIndex = 1,
        int pageSize = 12,
        string? category = null,
        string? brand = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? searchTerm = null,
        string? sortBy = "newest") // ← AJOUTEZ CE PARAMÈTRE
    {
        Console.WriteLine("=== DEBUG GetPaginatedProductsAsync ===");
        Console.WriteLine($"SortBy: {sortBy}");

        var query = _context.Products
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.StockQuantity,
                p.Category,
                p.Brand,
                p.ImageUrl,
                Rating = p.Rating,
                p.CreatedAt,
                p.IsActive,
                ReviewCount = p.Reviews.Count
            })
            .AsQueryable();

        // Appliquer les filtres...
        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category == category);
        if (!string.IsNullOrEmpty(brand))
            query = query.Where(p => p.Brand == brand);
        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);
        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);
        if (!string.IsNullOrEmpty(searchTerm))
            query = query.Where(p => p.Name.Contains(searchTerm) ||
                                    p.Description.Contains(searchTerm) ||
                                    p.Category.Contains(searchTerm));

        // Appliquer le tri
        switch (sortBy)
        {
            case "price_asc":
                query = query.OrderBy(p => p.Price);
                break;
            case "price_desc":
                query = query.OrderByDescending(p => p.Price);
                break;
            case "rating":
                query = query.OrderByDescending(p => p.Rating);
                break;
            case "popular":
                query = query.OrderByDescending(p => p.ReviewCount);
                break;
            default: // "newest"
                query = query.OrderByDescending(p => p.CreatedAt);
                break;
        }

        var count = await query.CountAsync();
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Mapper vers ProductDto
        var mappedItems = items.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description ?? "",
            Price = p.Price,
            StockQuantity = p.StockQuantity,
            Category = p.Category ?? "",
            Brand = p.Brand ?? "",
            ImageUrl = p.ImageUrl,
            Rating = p.Rating,
            CreatedAt = p.CreatedAt,
            IsActive = p.IsActive,
            ReviewCount = p.ReviewCount,
            OldPrice = p.Price > 100 ? p.Price * 1.2m : (decimal?)null,
            DiscountPercentage = p.Price > 100 ? 20 : 0
        }).ToList();

        return new PaginatedList<ProductDto>
        {
            Items = mappedItems,
            PageIndex = pageIndex,
            TotalPages = (int)Math.Ceiling(count / (double)pageSize),
            TotalCount = count,
            PageSize = pageSize
        };
    }

    // Dans ProductService.cs, ajoutez cette méthode
    public async Task<List<ProductDto>> SearchProductsForChatAsync(string query, int limit = 5)
    {
        try
        {
            var products = await _context.Products
                .Where(p => p.IsActive &&
                           (p.Name.Contains(query) ||
                            p.Description.Contains(query) ||
                            p.Category.Contains(query) ||
                            p.Brand.Contains(query)))
                .OrderByDescending(p => p.Rating)
                .Take(limit)
                .ToListAsync();

            return _mapper.Map<List<ProductDto>>(products);
        }
        catch
        {
            return new List<ProductDto>();
        }
    }
}