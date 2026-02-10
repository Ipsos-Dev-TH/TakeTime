using AutoMapper;
using TakeTime.Reservation.Application.Commands;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Mappers;

/// <summary>
/// AutoMapper profile defining mappings between reservation domain entities,
/// commands, and DTOs.
/// </summary>
public sealed class ReservationMappingProfile : Profile
{
    public ReservationMappingProfile()
    {
        // CreateReservationDto -> CreateReservationCommand
        CreateMap<CreateReservationDto, CreateReservationCommand>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedBy, opt => opt.Ignore());

        // CreateReservationAccommodationData -> ReservationAccommodationDto
        CreateMap<CreateReservationAccommodationData, ReservationAccommodationDto>();

        // CreateReservationItemData -> ReservationItemDto
        CreateMap<CreateReservationItemData, ReservationItemDto>();

        // CreateReservationAccommodationDto -> CreateReservationAccommodationData
        CreateMap<CreateReservationAccommodationDto, CreateReservationAccommodationData>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(_ => Guid.NewGuid()))
            .ForMember(dest => dest.AccommodationName, opt => opt.Ignore())
            .ForMember(dest => dest.AccommodationType, opt => opt.Ignore())
            .ForMember(dest => dest.RoomNumber, opt => opt.Ignore())
            .ForMember(dest => dest.NumberOfNights, opt => opt.Ignore())
            .ForMember(dest => dest.RatePerNight, opt => opt.Ignore())
            .ForMember(dest => dest.TotalRate, opt => opt.Ignore())
            .ForMember(dest => dest.Currency, opt => opt.Ignore());

        // CreateReservationItemDto -> CreateReservationItemData
        CreateMap<CreateReservationItemDto, CreateReservationItemData>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(_ => Guid.NewGuid()))
            .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.UnitPrice * src.Quantity))
            .ForMember(dest => dest.Currency, opt => opt.Ignore());

        // AccommodationDto self-mapping for updates
        CreateMap<CreateAccommodationDto, AccommodationDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(_ => Guid.NewGuid()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(_ => "Active"))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

        CreateMap<UpdateAccommodationDto, AccommodationDto>()
            .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember is not null));

        // ReservationSummaryDto from full ReservationDto
        CreateMap<ReservationDto, ReservationSummaryDto>()
            .ForMember(dest => dest.AccommodationNames, opt => opt.MapFrom(
                src => string.Join(", ", src.Accommodations.Select(a => a.AccommodationName))));
    }
}
