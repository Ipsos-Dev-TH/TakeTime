using MediatR;

namespace TakeTime.CRM.Application.Commands;

public class DeleteCustomerCommand : IRequest<bool>
{
    public Guid CustomerId { get; set; }
}

public class DeleteCustomerHandler : IRequestHandler<DeleteCustomerCommand, bool>
{
    public async Task<bool> Handle(DeleteCustomerCommand request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Load customer from CRMDbContext by ID
        // 2. If not found, return false
        // 3. Call customer.MarkAsDeleted() for soft-delete
        // 4. Save changes
        // 5. Return true

        return await Task.FromResult(true);
    }
}
