using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Infrastructure.Repositories;

namespace TakeTime.CRM.Application.Queries;

public class GetCustomerReservationsQuery : IRequest<CustomerReservationHistoryDto>
{
    public Guid CustomerId { get; set; }
}

public class GetCustomerReservationsHandler : IRequestHandler<GetCustomerReservationsQuery, CustomerReservationHistoryDto>
{
    private readonly CRMDbContext _db;
    private readonly ILogger<GetCustomerReservationsHandler> _logger;

    public GetCustomerReservationsHandler(CRMDbContext db, ILogger<GetCustomerReservationsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerReservationHistoryDto> Handle(GetCustomerReservationsQuery request, CancellationToken ct)
    {
        // Verify customer exists and load basic info
        var customer = await _db.Customers
            .Where(c => c.Id == request.CustomerId)
            .Select(c => new { c.Id, c.FirstName, c.LastName, c.TotalVisits, TotalSpent = c.TotalSpent.Amount })
            .FirstOrDefaultAsync(ct);

        if (customer is null)
            throw new InvalidOperationException($"Customer with ID '{request.CustomerId}' not found.");

        // Note: Reservation data lives in the Reservation microservice.
        // In a full implementation, this would call the Reservation service via HTTP client
        // or query a read-model/materialized view.
        // For now, we return the customer stats from the CRM side.
        _logger.LogInformation(
            "Fetching reservation history for customer {CustomerId}. Cross-service query required.",
            request.CustomerId);

        return new CustomerReservationHistoryDto
        {
            CustomerId = customer.Id,
            CustomerName = $"{customer.FirstName} {customer.LastName}",
            TotalReservations = customer.TotalVisits,
            TotalSpent = customer.TotalSpent,
            Currency = "THB",
            Reservations = []
        };
    }
}
