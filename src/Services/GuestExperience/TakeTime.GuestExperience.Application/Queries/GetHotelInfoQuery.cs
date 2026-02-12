using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Queries;

/// <summary>
/// Query to retrieve hotel information for the guest self-service portal.
/// Returns about info, facilities, nearby places, and emergency contacts
/// configured for the current tenant.
/// </summary>
public sealed class GetHotelInfoQuery : IRequest<HotelInfoDto>
{
}

/// <summary>
/// Handler for <see cref="GetHotelInfoQuery"/>. Returns hotel information
/// sourced from tenant settings and configurable defaults. In a production
/// system this would pull from a CMS or database; currently provides
/// tenant-configured data with sensible defaults.
/// </summary>
public sealed class GetHotelInfoQueryHandler : IRequestHandler<GetHotelInfoQuery, HotelInfoDto>
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetHotelInfoQueryHandler> _logger;

    public GetHotelInfoQueryHandler(
        ICurrentTenantService currentTenantService,
        ILogger<GetHotelInfoQueryHandler> logger)
    {
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public Task<HotelInfoDto> Handle(GetHotelInfoQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogDebug("Fetching hotel info for tenant {TenantId}", tenantId);

        var hotelName = _currentTenantService.GetSetting("BusinessName") ?? "TakeTime Resort";
        var address = _currentTenantService.GetSetting("BusinessAddress");
        var phone = _currentTenantService.GetSetting("BusinessPhone");
        var email = _currentTenantService.GetSetting("BusinessEmail");

        var hotelInfo = new HotelInfoDto
        {
            HotelName = hotelName,
            Description = _currentTenantService.GetSetting("HotelDescription")
                ?? $"Welcome to {hotelName}. We look forward to making your stay memorable.",
            Address = address,
            Phone = phone,
            Email = email,
            Website = _currentTenantService.GetSetting("Website"),
            CheckInTime = _currentTenantService.GetSetting("CheckInTime") ?? "14:00",
            CheckOutTime = _currentTenantService.GetSetting("CheckOutTime") ?? "12:00",
            Facilities =
            [
                new FacilityDto { Name = "Swimming Pool", Description = "Outdoor swimming pool", OperatingHours = "07:00 - 20:00", IsComplimentary = true },
                new FacilityDto { Name = "Restaurant", Description = "Thai and international cuisine", OperatingHours = "06:30 - 22:00", IsComplimentary = false },
                new FacilityDto { Name = "WiFi", Description = "High-speed wireless internet", IsComplimentary = true },
                new FacilityDto { Name = "Parking", Description = "Free on-site parking", IsComplimentary = true }
            ],
            NearbyPlaces =
            [
                new NearbyPlaceDto { Name = "Local Market", Category = "Shopping", Distance = "500m" },
                new NearbyPlaceDto { Name = "Beach", Category = "Attraction", Distance = "1km" },
                new NearbyPlaceDto { Name = "Hospital", Category = "Medical", Distance = "3km" }
            ],
            EmergencyContacts =
            [
                new EmergencyContactDto { Name = "Front Desk", Phone = phone ?? "0-0000-0000", Description = "Available 24/7" },
                new EmergencyContactDto { Name = "Emergency Services", Phone = "191", Description = "Police / Ambulance" },
                new EmergencyContactDto { Name = "Tourist Police", Phone = "1155", Description = "English-speaking assistance" }
            ],
            AdditionalInfo = new Dictionary<string, string>
            {
                ["Currency"] = _currentTenantService.GetSetting("Currency") ?? "THB",
                ["Language"] = "Thai, English"
            }
        };

        return Task.FromResult(hotelInfo);
    }
}
