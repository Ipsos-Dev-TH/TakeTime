using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to update an existing accommodation. Supports partial updates where
/// only provided fields are modified.
/// </summary>
public sealed class UpdateAccommodationCommand : IRequest<AccommodationDto>
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? AccommodationType { get; set; }
    public string? RoomNumber { get; set; }
    public string? Floor { get; set; }
    public string? Building { get; set; }
    public string? Description { get; set; }
    public int? MaxOccupancy { get; set; }
    public int? DefaultOccupancy { get; set; }
    public decimal? BaseRatePerNight { get; set; }
    public string? Currency { get; set; }
    public bool? IsActive { get; set; }
    public List<string>? Amenities { get; set; }
    public List<string>? ImageUrls { get; set; }
    public decimal? AreaSqMeters { get; set; }
    public string? BedConfiguration { get; set; }
    public int? SortOrder { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="UpdateAccommodationCommand"/>. Retrieves the existing
/// accommodation entity, applies the provided changes, validates constraints,
/// and persists the update.
/// </summary>
public sealed class UpdateAccommodationCommandHandler : IRequestHandler<UpdateAccommodationCommand, AccommodationDto>
{
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateAccommodationCommandHandler> _logger;

    public UpdateAccommodationCommandHandler(
        IAccommodationRepository accommodationRepository,
        ITenantContext tenantContext,
        ILogger<UpdateAccommodationCommandHandler> logger)
    {
        _accommodationRepository = accommodationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<AccommodationDto> Handle(UpdateAccommodationCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "Updating accommodation {AccommodationId} for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        // Retrieve the existing entity
        var accommodation = await _accommodationRepository.GetEntityByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Accommodation with ID '{request.Id}' not found.");

        // Apply updates using domain methods
        var currency = request.Currency ?? tenantSettings.Currency ?? "THB";
        var name = request.Name ?? accommodation.Name;
        var description = request.Description ?? accommodation.Description;
        var maxGuests = request.MaxOccupancy ?? accommodation.MaxGuests;
        var basePrice = request.BaseRatePerNight.HasValue
            ? Money.Create(request.BaseRatePerNight.Value, currency)
            : accommodation.BasePrice;

        accommodation.Update(
            name,
            description,
            maxGuests,
            basePrice,
            request.Floor ?? accommodation.Floor,
            request.Building ?? accommodation.Building);

        accommodation.UpdatedBy = request.UpdatedBy;

        // Update amenities if provided
        if (request.Amenities is not null)
        {
            // Clear existing and re-add (using domain methods)
            var existingAmenities = accommodation.Amenities.ToList();
            foreach (var amenity in existingAmenities)
            {
                accommodation.RemoveAmenity(amenity);
            }
            foreach (var amenity in request.Amenities)
            {
                accommodation.AddAmenity(amenity);
            }
        }

        // Update images if provided
        if (request.ImageUrls is not null)
        {
            var existingImages = accommodation.Images.ToList();
            foreach (var image in existingImages)
            {
                accommodation.RemoveImage(image);
            }
            foreach (var imageUrl in request.ImageUrls)
            {
                accommodation.AddImage(imageUrl);
            }
        }

        // Handle soft-delete toggle
        if (request.IsActive.HasValue && !request.IsActive.Value)
        {
            accommodation.MarkAsDeleted(request.UpdatedBy);
        }
        else if (request.IsActive.HasValue && request.IsActive.Value && accommodation.IsDeleted)
        {
            accommodation.Restore();
        }

        // Persist
        await _accommodationRepository.UpdateAsync(accommodation, cancellationToken);

        _logger.LogInformation(
            "Accommodation {AccommodationId} updated for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        return await _accommodationRepository.GetAccommodationDtoByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve updated accommodation.");
    }
}
