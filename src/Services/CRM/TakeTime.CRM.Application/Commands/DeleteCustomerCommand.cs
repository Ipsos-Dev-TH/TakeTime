using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.CRM.Infrastructure.Repositories;

namespace TakeTime.CRM.Application.Commands;

public class DeleteCustomerCommand : IRequest<bool>
{
    public Guid CustomerId { get; set; }
}

public class DeleteCustomerHandler : IRequestHandler<DeleteCustomerCommand, bool>
{
    private readonly CRMDbContext _db;
    private readonly ILogger<DeleteCustomerHandler> _logger;

    public DeleteCustomerHandler(CRMDbContext db, ILogger<DeleteCustomerHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteCustomerCommand request, CancellationToken ct)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct);
        if (customer is null)
            return false;

        // Soft-delete via Entity base method
        customer.MarkAsDeleted();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Soft-deleted customer {CustomerCode} ({FullName})", customer.CustomerCode, customer.FullName);

        return true;
    }
}
