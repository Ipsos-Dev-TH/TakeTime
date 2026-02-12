using MediatR;
using TakeTime.CRM.Application.DTOs;

namespace TakeTime.CRM.Application.Queries;

public class GetCustomerReservationsQuery : IRequest<CustomerReservationHistoryDto>
{
    public Guid CustomerId { get; set; }
}

public class GetCustomerReservationsHandler : IRequestHandler<GetCustomerReservationsQuery, CustomerReservationHistoryDto>
{
    public async Task<CustomerReservationHistoryDto> Handle(GetCustomerReservationsQuery request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Verify customer exists in CRMDbContext
        // 2. Query reservation data via cross-service communication (e.g., HTTP client to Reservation service)
        //    or via a read-model/materialized view
        // 3. Return reservation history with summary stats

        return await Task.FromResult(new CustomerReservationHistoryDto
        {
            CustomerId = request.CustomerId,
            Reservations = [],
            TotalReservations = 0,
            TotalSpent = 0
        });
    }
}
