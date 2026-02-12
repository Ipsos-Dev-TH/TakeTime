using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Domain.Entities;
using TakeTime.CRM.Domain.Enums;
using TakeTime.CRM.Infrastructure.Repositories;

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
    private readonly CRMDbContext _db;
    private readonly ILogger<UpsertCustomerHandler> _logger;

    public UpsertCustomerHandler(CRMDbContext db, ILogger<UpsertCustomerHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerDto> Handle(UpsertCustomerCommand request, CancellationToken ct)
    {
        Customer? customer = null;

        // Try to find existing customer by ID, phone, or email
        if (request.ExistingCustomerId.HasValue)
        {
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.ExistingCustomerId.Value, ct);
        }

        if (customer is null && !string.IsNullOrWhiteSpace(request.Phone))
        {
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == request.Phone, ct);
        }

        if (customer is null && !string.IsNullOrWhiteSpace(request.Email))
        {
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == request.Email, ct);
        }

        if (customer is not null)
        {
            // Update existing customer
            customer.UpdateProfile(
                firstName: request.FirstName,
                lastName: request.LastName,
                phone: request.Phone ?? customer.Phone,
                email: request.Email,
                dateOfBirth: request.DateOfBirth
            );

            // Update tags: clear existing and add new ones
            if (request.Tags is not null)
            {
                // Remove tags that are no longer in the request
                foreach (var existingTag in customer.Tags.ToList())
                {
                    if (!request.Tags.Contains(existingTag, StringComparer.OrdinalIgnoreCase))
                        customer.RemoveTag(existingTag);
                }

                // Add new tags
                foreach (var tag in request.Tags)
                {
                    customer.AddTag(tag);
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Updated customer {CustomerCode} ({FullName})", customer.CustomerCode, customer.FullName);
        }
        else
        {
            // Parse customer source
            if (!Enum.TryParse<CustomerSource>(request.Source, true, out var source))
                source = CustomerSource.Direct;

            // Generate customer code
            var customerCode = $"CUS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

            customer = new Customer(
                customerCode: customerCode,
                firstName: request.FirstName,
                lastName: request.LastName,
                phone: request.Phone ?? string.Empty,
                source: source,
                email: request.Email,
                dateOfBirth: request.DateOfBirth
            );

            // Add tags
            if (request.Tags is not null)
            {
                foreach (var tag in request.Tags)
                {
                    customer.AddTag(tag);
                }
            }

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Created customer {CustomerCode} ({FullName})", customer.CustomerCode, customer.FullName);
        }

        return new CustomerDto
        {
            Id = customer.Id,
            CustomerCode = customer.CustomerCode,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Phone = customer.Phone,
            Email = customer.Email,
            Source = customer.Source.ToString(),
            DateOfBirth = customer.DateOfBirth,
            Tags = customer.Tags.ToList(),
            LoyaltyTier = customer.LoyaltyTier.ToString(),
            LoyaltyPoints = customer.LoyaltyPoints,
            TotalSpent = customer.TotalSpent.Amount,
            TotalVisits = customer.TotalVisits,
            FirstVisitDate = customer.FirstVisitDate,
            LastVisitDate = customer.LastVisitDate,
            CreatedAt = customer.CreatedAt
        };
    }
}
