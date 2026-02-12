using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Application.Interfaces;
using TakeTime.GuestExperience.Domain.Entities;
using TakeTime.Infrastructure.Database;

namespace TakeTime.GuestExperience.Infrastructure.Repositories;

public class GuestExperienceDbContext : BaseDbContext
{
    public DbSet<HousekeepingTask> HousekeepingTasks => Set<HousekeepingTask>();
    public DbSet<RoomServiceOrder> RoomServiceOrders => Set<RoomServiceOrder>();
    public DbSet<RoomServiceOrderItem> RoomServiceOrderItems => Set<RoomServiceOrderItem>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();

    public GuestExperienceDbContext(
        DbContextOptions<GuestExperienceDbContext> options,
        ICurrentTenantService tenantService)
        : base(options, tenantService)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<HousekeepingTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.RoomNumber, e.ScheduledAt });
            entity.Property(e => e.RoomNumber).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<RoomServiceOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.OrderNumber }).IsUnique();
            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SubTotal).HasPrecision(18, 2);
            entity.Property(e => e.VATAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.HasMany(e => e.Items).WithOne().HasForeignKey(i => i.OrderId);
        });

        modelBuilder.Entity<RoomServiceOrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.TotalPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<MaintenanceRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.RequestNumber }).IsUnique();
            entity.Property(e => e.RequestNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.EstimatedCost).HasPrecision(18, 2);
            entity.Property(e => e.ActualCost).HasPrecision(18, 2);
        });
    }
}
