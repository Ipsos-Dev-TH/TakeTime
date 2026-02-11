using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Infrastructure.Database;
using TakeTime.Inventory.Domain.Entities;

namespace TakeTime.Inventory.Infrastructure.Repositories;

public class InventoryDbContext : BaseDbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<SalesTransaction> SalesTransactions => Set<SalesTransaction>();
    public DbSet<SalesTransactionItem> SalesTransactionItems => Set<SalesTransactionItem>();

    public InventoryDbContext(
        DbContextOptions<InventoryDbContext> options,
        ICurrentTenantService tenantService,
        ICurrentUserService userService)
        : base(options, tenantService, userService)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.Barcode }).IsUnique().HasFilter("Barcode IS NOT NULL");
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Barcode).HasMaxLength(50);
            entity.Property(e => e.CostPrice).HasPrecision(18, 2);
            entity.Property(e => e.SellingPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.ProductId });
            entity.Property(e => e.Reason).HasMaxLength(500);
        });

        modelBuilder.Entity<SalesTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.TransactionNumber }).IsUnique();
            entity.Property(e => e.TransactionNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SubTotal).HasPrecision(18, 2);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
            entity.Property(e => e.VATAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.HasMany(e => e.Items).WithOne().HasForeignKey(i => i.SalesTransactionId);
        });

        modelBuilder.Entity<SalesTransactionItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalPrice).HasPrecision(18, 2);
        });
    }
}
