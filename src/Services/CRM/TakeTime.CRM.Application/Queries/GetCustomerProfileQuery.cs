using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Infrastructure.Repositories;

namespace TakeTime.CRM.Application.Queries;

public class GetCustomerProfileQuery : IRequest<CustomerDto?>
{
    public Guid CustomerId { get; set; }
}

public class GetCustomerProfileHandler : IRequestHandler<GetCustomerProfileQuery, CustomerDto?>
{
    private readonly CRMDbContext _db;
    private readonly ILogger<GetCustomerProfileHandler> _logger;

    public GetCustomerProfileHandler(CRMDbContext db, ILogger<GetCustomerProfileHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerDto?> Handle(GetCustomerProfileQuery request, CancellationToken ct)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct);
        if (customer is null)
        {
            _logger.LogWarning("Customer with ID '{CustomerId}' not found.", request.CustomerId);
            return null;
        }

        return new CustomerDto
        {
            Id = customer.Id,
            CustomerCode = customer.CustomerCode,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Phone = customer.Phone,
            Email = customer.Email,
            LoyaltyTier = customer.LoyaltyTier.ToString(),
            LoyaltyPoints = customer.LoyaltyPoints,
            TotalSpent = customer.TotalSpent.Amount,
            Source = customer.Source.ToString(),
            FirstVisitDate = customer.FirstVisitDate,
            LastVisitDate = customer.LastVisitDate,
            TotalVisits = customer.TotalVisits,
            DateOfBirth = customer.DateOfBirth,
            Tags = customer.Tags.ToList(),
            CreatedAt = customer.CreatedAt
        };
    }
}
