using Microsoft.Extensions.Logging;
using TakeTime.ChannelManager.Application.Commands;

namespace TakeTime.ChannelManager.Infrastructure.ExternalServices;

/// <summary>
/// Factory for creating channel-specific API clients.
/// Currently returns a no-op stub client for all channel types.
/// Replace with real OTA API integrations (Agoda, Booking.com, Expedia, etc.) as needed.
/// </summary>
public sealed class ChannelApiClientFactory : IChannelApiClientFactory
{
    private readonly ILogger<ChannelApiClientFactory> _logger;

    public ChannelApiClientFactory(ILogger<ChannelApiClientFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IChannelApiClient CreateClient(string channelType)
    {
        _logger.LogInformation(
            "Creating API client for channel type '{ChannelType}' (stub implementation)",
            channelType);

        // When real OTA integrations are implemented, this factory should return
        // channel-specific clients based on channelType. For example:
        //
        // return channelType.ToLowerInvariant() switch
        // {
        //     "agoda"         => new AgodaApiClient(...),
        //     "bookingdotcom" => new BookingDotComApiClient(...),
        //     "expedia"       => new ExpediaApiClient(...),
        //     "traveloka"     => new TravelokaApiClient(...),
        //     "tripdotcom"    => new TripDotComApiClient(...),
        //     "airbnb"        => new AirbnbApiClient(...),
        //     _               => throw new NotSupportedException($"Channel type '{channelType}' is not supported.")
        // };

        return new NoOpChannelApiClient(_logger);
    }
}

/// <summary>
/// A no-op implementation of <see cref="IChannelApiClient"/> that logs operations
/// but does not communicate with any external OTA API. Used as a placeholder until
/// real channel integrations are implemented.
/// </summary>
internal sealed class NoOpChannelApiClient : IChannelApiClient
{
    private readonly ILogger _logger;

    public NoOpChannelApiClient(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<int> PushAvailabilityAsync(
        ChannelConnectionEntry connection,
        IReadOnlyList<RoomAvailabilityEntry> availability,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[NoOp] PushAvailabilityAsync called for channel '{ChannelName}' (type: {ChannelType}) " +
            "with {Count} availability entries. No data was sent to the OTA.",
            connection.ChannelName, connection.ChannelType, availability.Count);

        // Return the number of date entries that would have been synced
        return Task.FromResult(availability.Count);
    }

    /// <inheritdoc />
    public Task<int> PushRatesAsync(
        ChannelConnectionEntry connection,
        IReadOnlyList<RoomAvailabilityEntry> availability,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[NoOp] PushRatesAsync called for channel '{ChannelName}' (type: {ChannelType}) " +
            "with {Count} rate entries. No data was sent to the OTA.",
            connection.ChannelName, connection.ChannelType, availability.Count);

        // Return the number of rate entries that would have been synced
        return Task.FromResult(availability.Count);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ImportedReservationEntry>> FetchReservationsAsync(
        ChannelConnectionEntry connection,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[NoOp] FetchReservationsAsync called for channel '{ChannelName}' (type: {ChannelType}) " +
            "since {Since}. No reservations were fetched from the OTA.",
            connection.ChannelName, connection.ChannelType, since.ToString("yyyy-MM-dd HH:mm:ss"));

        return Task.FromResult<IReadOnlyList<ImportedReservationEntry>>(
            Array.Empty<ImportedReservationEntry>());
    }
}
