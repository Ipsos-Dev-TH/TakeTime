using MediatR;
using TakeTime.CRM.Application.DTOs;

namespace TakeTime.CRM.Application.Commands;

public class UpsertCustomerCommand : IRequest<CustomerDto>
{
    public Guid? ExistingCustomerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Source { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public List<string>? Tags { get; set; }
}

public class UpsertCustomerHandler : IRequestHandler<UpsertCustomerCommand, CustomerDto>
{
    public async Task<CustomerDto> Handle(UpsertCustomerCommand request, CancellationToken ct)
    {
        // In real implementation: search by phone/email to detect existing customer
        // If found, update; otherwise create new
        var isNew = !request.ExistingCustomerId.HasValue;
        var id = request.ExistingCustomerId ?? Guid.NewGuid();

        return await Task.FromResult(new CustomerDto
        {
            Id = id,
            CustomerCode = isNew ? $"CUS-{DateTime.UtcNow:yyyyMMdd}-{id.ToString()[..4].ToUpper()}" : string.Empty,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            Email = request.Email,
            Source = request.Source,
            DateOfBirth = request.DateOfBirth,
            Tags = request.Tags ?? [],
            LoyaltyTier = "Member",
            LoyaltyPoints = 0,
            TotalVisits = isNew ? 0 : 1,
            CreatedAt = DateTime.UtcNow
        });
    }
}
