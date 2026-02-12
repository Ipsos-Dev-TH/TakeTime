using MediatR;

namespace TakeTime.HumanResources.Application.Commands;

public class DeleteEmployeeCommand : IRequest<bool>
{
    public Guid EmployeeId { get; set; }
    public string? Reason { get; set; }
    public DateTime? TerminationDate { get; set; }
}

public class DeleteEmployeeHandler : IRequestHandler<DeleteEmployeeCommand, bool>
{
    public async Task<bool> Handle(DeleteEmployeeCommand request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Load employee from HRDbContext by ID
        // 2. If not found, return false
        // 3. Call employee.Terminate(terminationDate) to set status to Terminated
        //    and record the end date (soft-delete)
        // 4. Optionally trigger final payroll calculation
        // 5. Save changes

        var terminationDate = request.TerminationDate ?? DateTime.UtcNow;

        // Placeholder: in real implementation, load employee and call Terminate()
        return await Task.FromResult(true);
    }
}
