using AutoMapper;
using E_commerce.Models.DTOs;
using E_commerce.Models.Entities;

namespace E_commerce.Models.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Product mappings
            CreateMap<Product, ProductDto>()
                .ForMember(dest => dest.ReviewCount,
                    opt => opt.MapFrom(src => src.Reviews != null ? src.Reviews.Count : 0))
                .ForMember(dest => dest.OldPrice,
                    opt => opt.MapFrom(src => src.Price > 100 ? src.Price * 1.2m : (decimal?)null))
                .ForMember(dest => dest.DiscountPercentage,
                    opt => opt.MapFrom(src => src.Price > 100 ? 20 : 0));

            CreateMap<ProductDto, Product>()
                .ForMember(dest => dest.Reviews, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore());
        }
    }


}