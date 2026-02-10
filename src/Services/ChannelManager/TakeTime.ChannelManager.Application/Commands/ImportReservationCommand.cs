using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.ChannelManager.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.ChannelManager.Application.Commands;

/// <summary>
/// Command to import reservations from an OTA channel. Connects to the channel's API,
/// fetches new or updated reservations since the last sync, and creates local reservation
/// records for each imported booking.
/// </summary>
public sealed class ImportReservationCommand : IRequest<List<ImportedReservationDto>>
{
    public Guid ChannelConnectionId { get; set; }
    public DateTime? Since { get; set; }
}

/// <summary>
/// Handler for <see cref="ImportReservationCommand"/>. Fetches reservations from the OTA
/// channel, deduplicates against already-imported bookings, and creates local records.
/// </summary>
public sealed class ImportReservationCommandHandler : IRequestHandler<ImportReservationCommand, List<ImportedReservationDto>>
{
    private readonly IChannelManagerRepository _repository;
    private readonly IChannelApiClientFactory _channelApiClientFactory;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<ImportReservationCommandHandler> _logger;

    public ImportReservationCommandHandler(
        IChannelManagerRepository repository,
        IChannelApiClientFactory channelApiClientFactory,
        ICurrentTenantService currentTenantService,
        ILogger<ImportReservationCommandHandler> logger)
    {
        _repository = repository;
        _channelApiClientFactory = channelApiClientFactory;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<List<ImportedReservationDto>> Handle(ImportReservationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Importing reservations from channel connection {ChannelConnectionId}",
            request.ChannelConnectionId);

        // Retrieve channel connection details
        var connection = await _repository.GetChannelConnectionAsync(request.ChannelConnectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Channel connection {request.ChannelConnectionId} not found.");

        if (!connection.IsActive)
        {
            throw new InvalidOperationException($"Channel connection '{connection.ChannelName}' is not active.");
        }

        var since = request.Since ?? connection.LastSyncAt ?? DateTime.UtcNow.AddDays(-7);

        // Log the sync start
        var syncLogId = await _repository.CreateSyncLogAsync(new ChannelSyncLogEntry
        {
            ChannelConnectionId = request.ChannelConnectionId,
            TenantId = tenantId,
            SyncType = "Reservation",
            Direction = "Inbound",
            Status = "InProgress"
        }, cancellationToken);

        try
        {
            // Fetch reservations from the OTA
            var apiClient = _channelApiClientFactory.CreateClient(connection.ChannelType);
            var externalReservations = await apiClient.FetchReservationsAsync(connection, since, cancellationToken);

            var importedResults = new List<ImportedReservationDto>();

            foreach (var extReservation in externalReservations)
            {
                extReservation.TenantId = tenantId;

                var importId = await _repository.CreateImportedReservationAsync(extReservation, cancellationToken);

                importedResults.Add(new ImportedReservationDto
                {
                    Id = importId,
                    ChannelName = extReservation.ChannelName,
                    ExternalReservationId = extReservation.ExternalReservationId,
                    GuestName = extReservation.GuestName,
                    GuestEmail = extReservation.GuestEmail,
                    GuestPhone = extReservation.GuestPhone,
                    CheckInDate = extReservation.CheckInDate,
                    CheckOutDate = extReservation.CheckOutDate,
                    NumberOfGuests = extReservation.NumberOfGuests,
                    RoomType = extReservation.RoomType,
                    TotalAmount = extReservation.TotalAmount,
                    Currency = extReservation.Currency,
                    Status = extReservation.Status,
                    LocalReservationId = extReservation.LocalReservationId,
                    ImportedAt = extReservation.ImportedAt
                });
            }

            // Update sync log
            await _repository.UpdateSyncLogAsync(syncLogId, "Completed", importedResults.Count, null, cancellationToken);
            await _repository.UpdateConnectionSyncStatusAsync(
                request.ChannelConnectionId, DateTime.UtcNow, "Success", cancellationToken);

            _logger.LogInformation(
                "Imported {Count} reservations from {ChannelName}",
                importedResults.Count, connection.ChannelName);

            return importedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reservation import failed for channel connection {ChannelConnectionId}", request.ChannelConnectionId);

            await _repository.UpdateSyncLogAsync(syncLogId, "Failed", 0, ex.Message, cancellationToken);
            await _repository.UpdateConnectionSyncStatusAsync(
                request.ChannelConnectionId, DateTime.UtcNow, "Failed", cancellationToken);

            throw;
        }
    }
}
