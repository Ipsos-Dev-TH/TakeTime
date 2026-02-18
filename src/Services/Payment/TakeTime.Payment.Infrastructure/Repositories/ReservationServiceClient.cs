using Microsoft.Extensions.Logging;
using TakeTime.Payment.Application.Commands;

namespace TakeTime.Payment.Infrastructure.Repositories;

/// <summary>
/// Infrastructure implementation of <see cref="IReservationServiceClient"/>.
/// Intended to call the Reservation microservice over HTTP for cross-service
/// data retrieval and balance updates. Currently returns placeholder data
/// because inter-service communication (service discovery, authentication,
/// circuit-breaking) is not yet configured.
/// </summary>
public sealed class ReservationServiceClient : IReservationServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReservationServiceClient> _logger;

    public ReservationServiceClient(
        IHttpClientFactory httpClientFactory,
        ILogger<ReservationServiceClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ReservationService");
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ReservationInfoDto?> GetReservationAsync(
        Guid reservationId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "ReservationServiceClient: Inter-service communication not yet configured. " +
            "Returning placeholder for reservation {ReservationId}.",
            reservationId);

        // Return placeholder data until inter-service HTTP calls are configured.
        var placeholder = new ReservationInfoDto
        {
            Id = reservationId,
            ReservationNumber = $"RES-{reservationId.ToString()[..8].ToUpper()}",
            CustomerName = "Guest",
            TotalAmount = 0,
            AmountPaid = 0,
            BalanceDue = 0,
            Currency = "THB",
            Status = "Confirmed",
            Items = []
        };

        return Task.FromResult<ReservationInfoDto?>(placeholder);
    }

    /// <inheritdoc />
    public Task UpdateReservationBalanceAsync(
        Guid reservationId, decimal amountPaid, decimal balanceDue,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "ReservationServiceClient: Inter-service communication not yet configured. " +
            "Skipping balance update for reservation {ReservationId}. " +
            "AmountPaid={AmountPaid}, BalanceDue={BalanceDue}.",
            reservationId, amountPaid, balanceDue);

        return Task.CompletedTask;
    }
}
