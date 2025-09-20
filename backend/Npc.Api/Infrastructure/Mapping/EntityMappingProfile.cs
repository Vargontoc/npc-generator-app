using AutoMapper;
using Npc.Api.Entities;
using Npc.Api.Dtos;

namespace Npc.Api.Infrastructure.Mapping
{
    public class EntityMappingProfile : Profile
    {
        public EntityMappingProfile()
        {
            // Character mappings - based on actual Character entity
            CreateMap<Character, CharacterResponse>()
                .ForMember(dest => dest.UpdateAt, opt => opt.MapFrom(src => src.UpdatedAt)); // Note: DTO has UpdateAt, entity has UpdatedAt

            CreateMap<CharacterRequest, Character>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            // World mappings - based on actual World entity
            CreateMap<World, WorldResponse>();

            CreateMap<WorldRequest, World>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.LoreEntries, opt => opt.Ignore());

            // Lore mappings - based on actual Lore entity (LoreResponse doesn't include generation fields)
            CreateMap<Lore, LoreResponse>();

            CreateMap<LoreRequest, Lore>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.World, opt => opt.Ignore())
                .ForMember(dest => dest.IsGenerated, opt => opt.MapFrom(_ => false))
                .ForMember(dest => dest.GenerationSource, opt => opt.Ignore())
                .ForMember(dest => dest.GenerationMeta, opt => opt.Ignore())
                .ForMember(dest => dest.GeneratedAt, opt => opt.Ignore());

        }
    }
}