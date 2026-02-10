using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Infrastructure.Database;

namespace TakeTime.Accounting.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core DbContext for the Accounting microservice.
/// Inherits from <see cref="BaseDbContext"/> for multi-tenant isolation,
/// soft-delete filtering, audit fields, and domain event dispatching.
/// </summary>
public sealed class AccountingDbContext : BaseDbContext
{
    public AccountingDbContext(
        DbContextOptions<AccountingDbContext> options,
        ICurrentTenantService currentTenantService,
        IMediator? mediator = null,
        ILogger<BaseDbContext>? logger = null)
        : base(options, currentTenantService, mediator, logger)
    {
    }

    /// <summary>
    /// Financial transactions (revenue entries).
    /// </summary>
    public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();

    /// <summary>
    /// Expense entries.
    /// </summary>
    public DbSet<ExpenseEntity> Expenses => Set<ExpenseEntity>();

    /// <summary>
    /// Journal entries for double-entry bookkeeping.
    /// </summary>
    public DbSet<JournalEntryEntity> JournalEntries => Set<JournalEntryEntity>();

    /// <summary>
    /// Chart of accounts.
    /// </summary>
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();

    protected override void ApplyEntityConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("accounting");

        modelBuilder.Entity<TransactionEntity>(entity =>
        {
            entity.ToTable("Transactions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.TenantId, e.TransactionDate });
            entity.HasIndex(e => new { e.TenantId, e.Category });
        });

        modelBuilder.Entity<ExpenseEntity>(entity =>
        {
            entity.ToTable("Expenses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.TenantId, e.ExpenseDate });
        });

        modelBuilder.Entity<JournalEntryEntity>(entity =>
        {
            entity.ToTable("JournalEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DebitAmount).HasPrecision(18, 4);
            entity.Property(e => e.CreditAmount).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.TenantId, e.EntryDate });
        });

        modelBuilder.Entity<AccountEntity>(entity =>
        {
            entity.ToTable("Accounts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.AccountCode }).IsUnique();
        });
    }
}

/// <summary>
/// Transaction entity representing a financial transaction (revenue).
/// </summary>
public sealed class TransactionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? TenantId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? PaymentId { get; set; }
    public string Currency { get; set; } = "THB";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Expense entity representing a business expense.
/// </summary>
public sealed class ExpenseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? TenantId { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? Vendor { get; set; }
    public string? ReceiptReference { get; set; }
    public string Currency { get; set; } = "THB";
    public string? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Journal entry entity for double-entry bookkeeping.
/// </summary>
public sealed class JournalEntryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? TenantId { get; set; }
    public DateTime EntryDate { get; set; }
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Account entity for the chart of accounts.
/// </summary>
public sealed class AccountEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? TenantId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? ParentAccountCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
