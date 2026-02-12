using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Domain.Enums;
using TakeTime.CRM.Infrastructure.Repositories;

namespace TakeTime.CRM.Application.Queries;

public class SearchCustomersQuery : IRequest<List<CustomerDto>>
{
    public string? Keyword { get; set; }
    public string? Tier { get; set; }
    public string? Source { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class SearchCustomersHandler : IRequestHandler<SearchCustomersQuery, List<CustomerDto>>
{
    private readonly CRMDbContext _db;
    private readonly ILogger<SearchCustomersHandler> _logger;

    public SearchCustomersHandler(CRMDbContext db, ILogger<SearchCustomersHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<CustomerDto>> Handle(SearchCustomersQuery request, CancellationToken ct)
    {
        var query = _db.Customers.AsQueryable();

        // Filter by keyword (matching name, phone, email, customer code)
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.ToLower();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(keyword) ||
                c.LastName.ToLower().Contains(keyword) ||
                c.Phone.Contains(keyword) ||
                (c.Email != null && c.Email.ToLower().Contains(keyword)) ||
                c.CustomerCode.ToLower().Contains(keyword));
        }

        // Filter by loyalty tier
        if (!string.IsNullOrWhiteSpace(request.Tier) &&
            Enum.TryParse<LoyaltyTier>(request.Tier, true, out var tierFilter))
        {
            query = query.Where(c => c.LoyaltyTier == tierFilter);
        }

        // Filter by source
        if (!string.IsNullOrWhiteSpace(request.Source) &&
            Enum.TryParse<CustomerSource>(request.Source, true, out var sourceFilter))
        {
            query = query.Where(c => c.Source == sourceFilter);
        }

        // Order by last visit date descending (most recent first)
        query = query.OrderByDescending(c => c.LastVisitDate ?? c.CreatedAt);

        // Apply pagination
        var skip = (Math.Max(1, request.Page) - 1) * request.PageSize;
        var customers = await query
            .Skip(skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return customers.Select(c => new CustomerDto
        {
            Id = c.Id,
            CustomerCode = c.CustomerCode,
            FirstName = c.FirstName,
            LastName = c.LastName,
            Phone = c.Phone,
            Email = c.Email,
            LoyaltyTier = c.LoyaltyTier.ToString(),
            LoyaltyPoints = c.LoyaltyPoints,
            TotalSpent = c.TotalSpent.Amount,
            Source = c.Source.ToString(),
            FirstVisitDate = c.FirstVisitDate,
            LastVisitDate = c.LastVisitDate,
            TotalVisits = c.TotalVisits,
            DateOfBirth = c.DateOfBirth,
            Tags = c.Tags.ToList(),
            CreatedAt = c.CreatedAt
        }).ToList();
    }
}
