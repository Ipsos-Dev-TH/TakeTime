using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Affiliate.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Affiliate.Application.Commands;

/// <summary>
/// Command to register a new affiliate partner. Generates a unique affiliate code,
/// validates the registration data, and creates the affiliate profile.
/// </summary>
public sealed class RegisterAffiliateCommand : IRequest<AffiliateDto>
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public decimal CommissionRatePercent { get; set; } = 10;
    public string CommissionType { get; set; } = "Percentage";
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? TaxId { get; set; }
}

/// <summary>
/// Handler for <see cref="RegisterAffiliateCommand"/>. Creates a new affiliate profile
/// with a unique affiliate code, stores it in the repository, and returns the created affiliate.
/// </summary>
public sealed class RegisterAffiliateCommandHandler : IRequestHandler<RegisterAffiliateCommand, AffiliateDto>
{
    private readonly IAffiliateRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<RegisterAffiliateCommandHandler> _logger;

    public RegisterAffiliateCommandHandler(
        IAffiliateRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<RegisterAffiliateCommandHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<AffiliateDto> Handle(RegisterAffiliateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Registering new affiliate {Name} ({Email}) for tenant {TenantId}",
            request.Name, request.Email, tenantId);

        // Check for duplicate email
        var existingAffiliate = await _repository.GetByEmailAsync(tenantId, request.Email, cancellationToken);
        if (existingAffiliate is not null)
        {
            throw new InvalidOperationException($"An affiliate with email '{request.Email}' already exists.");
        }

        // Generate unique affiliate code
        var affiliateCode = await GenerateAffiliateCodeAsync(tenantId, request.Name, cancellationToken);

        var affiliate = new AffiliateEntry
        {
            TenantId = tenantId,
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            CompanyName = request.CompanyName,
            AffiliateCode = affiliateCode,
            Status = "Active",
            CommissionRatePercent = request.CommissionRatePercent,
            CommissionType = request.CommissionType,
            BankAccountName = request.BankAccountName,
            BankAccountNumber = request.BankAccountNumber,
            BankName = request.BankName,
            TaxId = request.TaxId
        };

        var id = await _repository.CreateAsync(affiliate, cancellationToken);

        _logger.LogInformation(
            "Affiliate {AffiliateName} registered with code {AffiliateCode}",
            request.Name, affiliateCode);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        return new AffiliateDto
        {
            Id = id,
            TenantId = tenantId,
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            CompanyName = request.CompanyName,
            AffiliateCode = affiliateCode,
            Status = "Active",
            CommissionRatePercent = request.CommissionRatePercent,
            CommissionType = request.CommissionType,
            TotalEarnings = 0,
            PendingBalance = 0,
            PaidBalance = 0,
            TotalReferrals = 0,
            SuccessfulReferrals = 0,
            BankAccountName = request.BankAccountName,
            BankAccountNumber = request.BankAccountNumber,
            BankName = request.BankName,
            TaxId = request.TaxId,
            Currency = currency,
            CreatedAt = DateTime.UtcNow
        };
    }

    private async Task<string> GenerateAffiliateCodeAsync(string tenantId, string name, CancellationToken cancellationToken)
    {
        var prefix = name.Length >= 3
            ? name[..3].ToUpperInvariant().Replace(" ", "X")
            : name.ToUpperInvariant().PadRight(3, 'X');

        var count = await _repository.GetCountAsync(tenantId, cancellationToken);
        return $"AFF-{prefix}-{(count + 1):D4}";
    }
}

/// <summary>
/// Repository interface for affiliate operations.
/// </summary>
public interface IAffiliateRepository
{
    Task<Guid> CreateAsync(AffiliateEntry affiliate, CancellationToken cancellationToken = default);
    Task<AffiliateEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AffiliateEntry?> GetByEmailAsync(string tenantId, string email, CancellationToken cancellationToken = default);
    Task<AffiliateEntry?> GetByCodeAsync(string tenantId, string affiliateCode, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(string tenantId, CancellationToken cancellationToken = default);
    Task UpdateAsync(AffiliateEntry affiliate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AffiliateEntry>> SearchAsync(string tenantId, string? status, string? name, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Guid> CreateCommissionAsync(CommissionEntry commission, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommissionEntry>> GetCommissionsByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken = default);
    Task<Guid> CreatePayoutAsync(PayoutEntry payout, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayoutEntry>> GetPayoutsByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken = default);
    Task<(decimal totalCommission, int totalBookings, int totalReferrals, decimal conversionRate)> GetPerformanceMetricsAsync(string tenantId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}

/// <summary>
/// Internal affiliate entry model.
/// </summary>
public sealed class AffiliateEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public string AffiliateCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public decimal CommissionRatePercent { get; set; }
    public string CommissionType { get; set; } = "Percentage";
    public decimal TotalEarnings { get; set; }
    public decimal PendingBalance { get; set; }
    public decimal PaidBalance { get; set; }
    public int TotalReferrals { get; set; }
    public int SuccessfulReferrals { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? TaxId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Internal commission entry model.
/// </summary>
public sealed class CommissionEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AffiliateId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public decimal ReservationAmount { get; set; }
    public decimal CommissionRatePercent { get; set; }
    public decimal CommissionAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
}

/// <summary>
/// Internal payout entry model.
/// </summary>
public sealed class PayoutEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AffiliateId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? TransactionReference { get; set; }
    public string Status { get; set; } = "Pending";
    public string Currency { get; set; } = "THB";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? Notes { get; set; }
}
