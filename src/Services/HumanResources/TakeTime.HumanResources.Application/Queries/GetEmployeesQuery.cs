using MediatR;
using TakeTime.HumanResources.Application.DTOs;

namespace TakeTime.HumanResources.Application.Queries;

public class GetEmployeesQuery : IRequest<List<EmployeeDto>>
{
    public string? Keyword { get; set; }
    public string? Department { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetEmployeesHandler : IRequestHandler<GetEmployeesQuery, List<EmployeeDto>>
{
    public async Task<List<EmployeeDto>> Handle(GetEmployeesQuery request, CancellationToken ct)
    {
        // Placeholder: in real implementation, query from HRDbContext
        return await Task.FromResult(new List<EmployeeDto>());
    }
}
