using MediatR;
using TakeTime.HumanResources.Application.DTOs;

namespace TakeTime.HumanResources.Application.Queries;

public class GetEmployeeByIdQuery : IRequest<EmployeeDto?>
{
    public Guid EmployeeId { get; set; }
}

public class GetEmployeeByIdHandler : IRequestHandler<GetEmployeeByIdQuery, EmployeeDto?>
{
    public async Task<EmployeeDto?> Handle(GetEmployeeByIdQuery request, CancellationToken ct)
    {
        // Placeholder: in real implementation, query from HRDbContext
        // Should return full employee details including compensation, leave balances,
        // social security info, and manager information
        return await Task.FromResult<EmployeeDto?>(null);
    }
}
