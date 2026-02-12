using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Application.Interfaces;
using TakeTime.CRM.Domain.Entities;
using TakeTime.Infrastructure.Database;

namespace TakeTime.CRM.Infrastructure.Repositories;

public class CRMDbContext : BaseDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<GuestReview> GuestReviews => Set<GuestReview>();

    public CRMDbContext(
        DbContextOptions<CRMDbContext> options,
        ICurrentTenantService tenantService)
        : base(options, tenantService)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.CustomerCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Phone });
            entity.HasIndex(e => new { e.TenantId, e.Email });
            entity.Property(e => e.CustomerCode).HasMaxLength(30).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.TotalSpent).HasPrecision(18, 2);
        });

        modelBuilder.Entity<LoyaltyTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.CustomerId });
        });

        modelBuilder.Entity<GuestReview>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.CustomerId });
            entity.Property(e => e.Comment).HasMaxLength(2000);
            entity.Property(e => e.Response).HasMaxLength(2000);
        });
    }
}
