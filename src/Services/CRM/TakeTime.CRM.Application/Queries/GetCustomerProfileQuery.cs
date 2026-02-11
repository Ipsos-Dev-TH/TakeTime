using MediatR;
using TakeTime.CRM.Application.DTOs;

namespace TakeTime.CRM.Application.Queries;

public class GetCustomerProfileQuery : IRequest<CustomerDto?>
{
    public Guid CustomerId { get; set; }
}

public class GetCustomerProfileHandler : IRequestHandler<GetCustomerProfileQuery, CustomerDto?>
{
    public async Task<CustomerDto?> Handle(GetCustomerProfileQuery request, CancellationToken ct)
    {
        // Placeholder: query from CRMDbContext
        return await Task.FromResult<CustomerDto?>(null);
    }
}
