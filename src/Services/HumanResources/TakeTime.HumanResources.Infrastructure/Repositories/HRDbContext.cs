using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Application.Interfaces;
using TakeTime.HumanResources.Domain.Entities;
using TakeTime.Infrastructure.Database;

namespace TakeTime.HumanResources.Infrastructure.Repositories;

public class HRDbContext : BaseDbContext
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<OvertimeRecord> OvertimeRecords => Set<OvertimeRecord>();
    public DbSet<PayrollRecord> PayrollRecords => Set<PayrollRecord>();

    public HRDbContext(
        DbContextOptions<HRDbContext> options,
        ICurrentTenantService tenantService,
        ICurrentUserService userService)
        : base(options, tenantService, userService)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.EmployeeCode }).IsUnique();
            entity.Property(e => e.EmployeeCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Salary).HasPrecision(18, 2);
        });

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.StartDate });
        });

        modelBuilder.Entity<OvertimeRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Date });
            entity.Property(e => e.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PayrollRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Month, e.Year }).IsUnique();
            entity.Property(e => e.BaseSalary).HasPrecision(18, 2);
            entity.Property(e => e.OTAmount).HasPrecision(18, 2);
            entity.Property(e => e.GrossPay).HasPrecision(18, 2);
            entity.Property(e => e.TaxDeduction).HasPrecision(18, 2);
            entity.Property(e => e.SocialSecurity).HasPrecision(18, 2);
            entity.Property(e => e.NetPay).HasPrecision(18, 2);
        });
    }
}
