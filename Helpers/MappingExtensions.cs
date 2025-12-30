using AutoMapper;
using E_commerce.Models.DTOs;
using E_commerce.Models.Entities;

namespace E_commerce.Helpers
{
    public static class MappingExtensions
    {
        // Pour PaginatedList<Product> -> PaginatedList<ProductDto>
        public static PaginatedList<ProductDto> ToPaginatedDto(this PaginatedList<Product> source, IMapper mapper)
        {
            return new PaginatedList<ProductDto>
            {
                Items = mapper.Map<List<ProductDto>>(source.Items),
                PageIndex = source.PageIndex,
                TotalPages = source.TotalPages,
                TotalCount = source.TotalCount,
                PageSize = source.PageSize
            };
        }

        // Pour List<Product> -> List<ProductDto>
        public static List<ProductDto> ToDtoList(this List<Product> source, IMapper mapper)
        {
            return mapper.Map<List<ProductDto>>(source);
        }

        // Pour Product -> ProductDto
        public static ProductDto ToDto(this Product source, IMapper mapper)
        {
            return mapper.Map<ProductDto>(source);
        }

        // Pour Cart -> CartDto
        public static CartDto ToDto(this Cart source, IMapper mapper)
        {
            return mapper.Map<CartDto>(source);
        }

        // Pour CartDto -> Cart
        public static Cart ToEntity(this CartDto source, IMapper mapper)
        {
            return mapper.Map<Cart>(source);
        }

        // Pour PaginatedList<T> générique
        public static PaginatedList<TDestination> ToMappedPaginatedList<TSource, TDestination>(
            this PaginatedList<TSource> source, IMapper mapper)
        {
            return new PaginatedList<TDestination>
            {
                Items = mapper.Map<List<TDestination>>(source.Items),
                PageIndex = source.PageIndex,
                TotalPages = source.TotalPages,
                TotalCount = source.TotalCount,
                PageSize = source.PageSize
            };
        }
    }
}