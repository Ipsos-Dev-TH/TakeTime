using MediatR;
using TakeTime.CRM.Application.DTOs;

namespace TakeTime.CRM.Application.Commands;

public class MergeCustomersCommand : IRequest<CustomerDto>
{
    public Guid PrimaryCustomerId { get; set; }
    public List<Guid> DuplicateCustomerIds { get; set; } = [];
}

public class MergeCustomersHandler : IRequestHandler<MergeCustomersCommand, CustomerDto>
{
    public async Task<CustomerDto> Handle(MergeCustomersCommand request, CancellationToken ct)
    {
        if (request.DuplicateCustomerIds.Count == 0)
            throw new InvalidOperationException("At least one duplicate customer ID is required.");

        if (request.DuplicateCustomerIds.Contains(request.PrimaryCustomerId))
            throw new InvalidOperationException("Primary customer ID cannot be in the duplicate list.");

        // In real implementation:
        // 1. Load primary customer from CRMDbContext
        // 2. Load all duplicate customers
        // 3. Merge data into primary:
        //    - Sum loyalty points
        //    - Sum total spent
        //    - Sum total visits
        //    - Merge tags (union)
        //    - Merge preferences (union)
        //    - Merge notes
        //    - Keep earliest first visit date
        //    - Keep latest last visit date
        // 4. Re-assign loyalty transactions from duplicates to primary
        // 5. Re-assign guest reviews from duplicates to primary
        // 6. Soft-delete duplicate customers
        // 7. Save changes
        // 8. Return updated primary customer

        return await Task.FromResult(new CustomerDto
        {
            Id = request.PrimaryCustomerId,
            CreatedAt = DateTime.UtcNow
        });
    }
}
