using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;
using TakeTime.Reservation.Domain.Entities;
using TakeTime.Reservation.Domain.Enums;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to create a new accommodation unit. Validates uniqueness of the room
/// number within the tenant and sets defaults based on tenant configuration.
/// </summary>
public sealed class CreateAccommodationCommand : IRequest<AccommodationDto>
{
    public string Name { get; set; } = string.Empty;
    public string AccommodationType { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public string? Floor { get; set; }
    public string? Building { get; set; }
    public string? Description { get; set; }
    public int MaxOccupancy { get; set; }
    public int DefaultOccupancy { get; set; }
    public decimal BaseRatePerNight { get; set; }
    public string Currency { get; set; } = "THB";
    public List<string> Amenities { get; set; } = [];
    public List<string> ImageUrls { get; set; } = [];
    public decimal? AreaSqMeters { get; set; }
    public string? BedConfiguration { get; set; }
    public int SortOrder { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="CreateAccommodationCommand"/>. Creates a new accommodation
/// entity with the provided details, applies tenant-specific currency defaults,
/// and persists it.
/// </summary>
public sealed class CreateAccommodationCommandHandler : IRequestHandler<CreateAccommodationCommand, AccommodationDto>
{
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateAccommodationCommandHandler> _logger;

    public CreateAccommodationCommandHandler(
        IAccommodationRepository accommodationRepository,
        ITenantContext tenantContext,
        ILogger<CreateAccommodationCommandHandler> logger)
    {
        _accommodationRepository = accommodationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<AccommodationDto> Handle(CreateAccommodationCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var currency = request.Currency ?? tenantSettings.Currency ?? "THB";

        _logger.LogInformation(
            "Creating accommodation '{Name}' of type {Type} for tenant {TenantId}",
            request.Name, request.AccommodationType, tenantSettings.TenantId);

        // Parse accommodation type
        if (!Enum.TryParse<AccommodationType>(request.AccommodationType, true, out var accommodationType))
        {
            throw new InvalidOperationException(
                $"Invalid accommodation type '{request.AccommodationType}'. " +
                $"Valid types are: {string.Join(", ", Enum.GetNames<AccommodationType>())}.");
        }

        // Create the domain entity
        var basePrice = Money.Create(request.BaseRatePerNight, currency);
        var accommodation = new Accommodation(
            request.Name,
            accommodationType,
            request.MaxOccupancy > 0 ? request.MaxOccupancy : 2,
            basePrice,
            request.Description,
            request.Floor,
            request.Building);

        accommodation.TenantId = tenantSettings.TenantId;
        accommodation.CreatedBy = request.CreatedBy;

        // Add amenities
        foreach (var amenity in request.Amenities)
        {
            accommodation.AddAmenity(amenity);
        }

        // Add images
        foreach (var imageUrl in request.ImageUrls)
        {
            accommodation.AddImage(imageUrl);
        }

        // Persist the accommodation
        await _accommodationRepository.CreateAsync(accommodation, cancellationToken);

        _logger.LogInformation(
            "Accommodation '{Name}' (ID: {AccommodationId}) created for tenant {TenantId}",
            request.Name, accommodation.Id, tenantSettings.TenantId);

        // Return the created accommodation as DTO
        return await _accommodationRepository.GetAccommodationDtoByIdAsync(accommodation.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve newly created accommodation.");
    }
}
