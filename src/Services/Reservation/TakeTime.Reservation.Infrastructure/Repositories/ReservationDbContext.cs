using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Infrastructure.Database;
using TakeTime.Reservation.Domain.Entities;
using TakeTime.Reservation.Infrastructure.Configurations;

namespace TakeTime.Reservation.Infrastructure.Repositories;

/// <summary>
/// EF Core DbContext for the Reservation microservice.
/// Extends BaseDbContext to inherit multi-tenant isolation, soft-delete filtering,
/// automatic audit fields, and domain event dispatching.
/// </summary>
public class ReservationDbContext : BaseDbContext
{
    public DbSet<Domain.Entities.Reservation> Reservations => Set<Domain.Entities.Reservation>();
    public DbSet<Accommodation> Accommodations => Set<Accommodation>();
    public DbSet<ReservationAccommodation> ReservationAccommodations => Set<ReservationAccommodation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();
    public DbSet<ReservationPayment> ReservationPayments => Set<ReservationPayment>();
    public DbSet<RentalItem> RentalItems => Set<RentalItem>();
    public DbSet<RatePlan> RatePlans => Set<RatePlan>();
    public DbSet<SeasonalRateEntity> SeasonalRates => Set<SeasonalRateEntity>();
    public DbSet<Promotion> Promotions => Set<Promotion>();

    public ReservationDbContext(
        DbContextOptions<ReservationDbContext> options,
        ICurrentTenantService currentTenantService,
        IMediator? mediator = null,
        ILogger<ReservationDbContext>? logger = null)
        : base(options, currentTenantService, mediator, logger)
    {
    }

    protected override void ApplyEntityConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ReservationEntityConfiguration());

        // Accommodation configuration
        modelBuilder.Entity<Accommodation>(entity =>
        {
            entity.ToTable("Accommodations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Floor).HasMaxLength(20);
            entity.Property(e => e.Building).HasMaxLength(100);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(100);

            // Money value object for BasePrice
            entity.OwnsOne(e => e.BasePrice, money =>
            {
                money.Property(m => m.Amount).HasColumnName("BasePriceAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("BasePriceCurrency").HasMaxLength(3);
            });

            // Store amenities and images as JSON
            entity.Property(e => e.Amenities)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
                .HasColumnType("nvarchar(max)");

            entity.Property(e => e.Images)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
                .HasColumnType("nvarchar(max)");

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Type);

            entity.Property(e => e.Version).IsConcurrencyToken();
        });

        // ReservationAccommodation configuration
        modelBuilder.Entity<ReservationAccommodation>(entity =>
        {
            entity.ToTable("ReservationAccommodations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AccommodationName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RoomNumber).HasMaxLength(50);
            entity.Property(e => e.TenantId).HasMaxLength(100);

            entity.OwnsOne(e => e.PricePerNight, money =>
            {
                money.Property(m => m.Amount).HasColumnName("PricePerNightAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("PricePerNightCurrency").HasMaxLength(3);
            });

            entity.OwnsOne(e => e.TotalPrice, money =>
            {
                money.Property(m => m.Amount).HasColumnName("TotalPriceAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("TotalPriceCurrency").HasMaxLength(3);
            });

            entity.HasIndex(e => e.ReservationId);
            entity.HasIndex(e => e.AccommodationId);
        });

        // ReservationItem configuration
        modelBuilder.Entity<ReservationItem>(entity =>
        {
            entity.ToTable("ReservationItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TenantId).HasMaxLength(100);

            entity.OwnsOne(e => e.PricePerUnit, money =>
            {
                money.Property(m => m.Amount).HasColumnName("PricePerUnitAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("PricePerUnitCurrency").HasMaxLength(3);
            });

            entity.OwnsOne(e => e.TotalPrice, money =>
            {
                money.Property(m => m.Amount).HasColumnName("TotalPriceAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("TotalPriceCurrency").HasMaxLength(3);
            });

            entity.HasIndex(e => e.ReservationId);
        });

        // ReservationPayment configuration
        modelBuilder.Entity<ReservationPayment>(entity =>
        {
            entity.ToTable("ReservationPayments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PaymentMethod).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TenantId).HasMaxLength(100);

            entity.OwnsOne(e => e.Amount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("PaymentAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("PaymentCurrency").HasMaxLength(3);
            });

            entity.HasIndex(e => e.ReservationId);
            entity.HasIndex(e => e.PaymentId);
        });

        // RentalItem configuration
        modelBuilder.Entity<RentalItem>(entity =>
        {
            entity.ToTable("RentalItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.TenantId).HasMaxLength(100);

            entity.OwnsOne(e => e.PricePerDay, money =>
            {
                money.Property(m => m.Amount).HasColumnName("PricePerDayAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("PricePerDayCurrency").HasMaxLength(3);
            });

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.Category);
        });

        // RatePlan configuration
        modelBuilder.Entity<RatePlan>(entity =>
        {
            entity.ToTable("RatePlans");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.IsDefault).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(100);

            entity.OwnsOne(e => e.BaseRate, money =>
            {
                money.Property(m => m.Amount).HasColumnName("BaseRateAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("BaseRateCurrency").HasMaxLength(3);
            });

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.IsActive);
            entity.Property(e => e.Version).IsConcurrencyToken();
        });

        // SeasonalRateEntity configuration
        modelBuilder.Entity<SeasonalRateEntity>(entity =>
        {
            entity.ToTable("SeasonalRates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SeasonName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.RateMultiplier).HasPrecision(8, 4);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(100);

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.StartDate, e.EndDate });
            entity.Property(e => e.Version).IsConcurrencyToken();
        });

        // Promotion configuration
        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.ToTable("Promotions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.DiscountType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.DiscountValue).HasPrecision(18, 2);
            entity.Property(e => e.MinimumBookingAmount).HasPrecision(18, 2);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(100);

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.Code);
            entity.HasIndex(e => e.IsActive);
            entity.Property(e => e.Version).IsConcurrencyToken();
        });
    }
}
