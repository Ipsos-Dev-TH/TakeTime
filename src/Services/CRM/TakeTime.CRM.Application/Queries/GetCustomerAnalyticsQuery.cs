using MediatR;
using TakeTime.CRM.Application.DTOs;

namespace TakeTime.CRM.Application.Queries;

public class GetCustomerAnalyticsQuery : IRequest<CustomerAnalyticsDto>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class GetCustomerAnalyticsHandler : IRequestHandler<GetCustomerAnalyticsQuery, CustomerAnalyticsDto>
{
    public async Task<CustomerAnalyticsDto> Handle(GetCustomerAnalyticsQuery request, CancellationToken ct)
    {
        return await Task.FromResult(new CustomerAnalyticsDto());
    }
}
