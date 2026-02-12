using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.HumanResources.Domain.Enums;
using TakeTime.HumanResources.Infrastructure.Repositories;

namespace TakeTime.HumanResources.Application.Commands;

public class DeleteEmployeeCommand : IRequest<bool>
{
    public Guid EmployeeId { get; set; }
    public string? Reason { get; set; }
    public DateTime? TerminationDate { get; set; }
}

public class DeleteEmployeeHandler : IRequestHandler<DeleteEmployeeCommand, bool>
{
    private readonly HRDbContext _db;
    private readonly ILogger<DeleteEmployeeHandler> _logger;

    public DeleteEmployeeHandler(HRDbContext db, ILogger<DeleteEmployeeHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteEmployeeCommand request, CancellationToken ct)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct);
        if (employee is null)
            return false;

        var terminationDate = request.TerminationDate ?? DateTime.UtcNow;

        // Use domain method to terminate if employee is active, otherwise soft-delete
        if (employee.Status == EmployeeStatus.Active)
        {
            employee.Terminate(terminationDate);
        }

        // Soft-delete the record via Entity base method
        employee.MarkAsDeleted(request.Reason);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Soft-deleted employee {EmployeeCode} ({FullName}). Reason: {Reason}",
            employee.EmployeeCode, employee.FullName, request.Reason ?? "Not specified");

        return true;
    }
}
