using MediatR;
using TakeTime.CRM.Application.DTOs;

namespace TakeTime.CRM.Application.Queries;

public class SearchCustomersQuery : IRequest<List<CustomerDto>>
{
    public string? Keyword { get; set; }
    public string? Tier { get; set; }
    public string? Source { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class SearchCustomersHandler : IRequestHandler<SearchCustomersQuery, List<CustomerDto>>
{
    public async Task<List<CustomerDto>> Handle(SearchCustomersQuery request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Query Customers from CRMDbContext
        // 2. Filter by keyword (matching name, phone, email, customer code)
        // 3. Filter by loyalty tier if provided
        // 4. Filter by source if provided
        // 5. Apply pagination (skip/take)
        // 6. Order by last visit date descending
        // 7. Map to CustomerDto

        return await Task.FromResult(new List<CustomerDto>());
    }
}
