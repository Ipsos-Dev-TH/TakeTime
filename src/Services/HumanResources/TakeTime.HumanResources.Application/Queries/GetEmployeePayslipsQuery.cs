using MediatR;
using TakeTime.HumanResources.Application.DTOs;

namespace TakeTime.HumanResources.Application.Queries;

public class GetEmployeePayslipsQuery : IRequest<List<PayrollDto>>
{
    public Guid EmployeeId { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
}

public class GetEmployeePayslipsHandler : IRequestHandler<GetEmployeePayslipsQuery, List<PayrollDto>>
{
    public async Task<List<PayrollDto>> Handle(GetEmployeePayslipsQuery request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Query PayrollRecords from HRDbContext for the specified employee
        // 2. Filter by year and/or month if provided
        // 3. Include salary breakdown, OT calculations, social security, withholding tax
        // 4. Order by year descending, month descending
        // 5. Map to PayrollDto

        return await Task.FromResult(new List<PayrollDto>());
    }
}
