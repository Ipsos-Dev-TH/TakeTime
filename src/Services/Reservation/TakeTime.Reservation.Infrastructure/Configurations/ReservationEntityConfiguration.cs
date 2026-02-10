using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TakeTime.Reservation.Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the Reservation aggregate root.
/// Defines table mapping, property constraints, relationships, owned types, and indexes.
/// </summary>
public class ReservationEntityConfiguration : IEntityTypeConfiguration<Domain.Entities.Reservation>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Reservation> builder)
    {
        builder.ToTable("Reservations");

        // Primary key
        builder.HasKey(e => e.Id);

        // Reservation number - unique per tenant
        builder.Property(e => e.ReservationNumber)
            .IsRequired()
            .HasMaxLength(50);

        // Customer information
        builder.Property(e => e.CustomerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.CustomerPhone)
            .HasMaxLength(50);

        builder.Property(e => e.CustomerEmail)
            .HasMaxLength(200);

        // Dates
        builder.Property(e => e.CheckInDate)
            .IsRequired();

        builder.Property(e => e.CheckOutDate)
            .IsRequired();

        builder.Property(e => e.NumberOfNights)
            .IsRequired();

        // Status and source as string (enum stored as string for readability)
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Source)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        // Text fields
        builder.Property(e => e.SpecialRequests)
            .HasMaxLength(2000);

        builder.Property(e => e.InternalNotes)
            .HasMaxLength(4000);

        builder.Property(e => e.ChannelReservationId)
            .HasMaxLength(100);

        // Multi-tenancy
        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        // Audit fields
        builder.Property(e => e.CreatedBy).HasMaxLength(200);
        builder.Property(e => e.UpdatedBy).HasMaxLength(200);
        builder.Property(e => e.DeletedBy).HasMaxLength(200);

        // Money value objects (owned types)
        builder.OwnsOne(e => e.TotalAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("TotalAmount")
                .HasPrecision(18, 2)
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("TotalCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(e => e.DepositAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("DepositAmount")
                .HasPrecision(18, 2)
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("DepositCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(e => e.BalanceAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("BalanceAmount")
                .HasPrecision(18, 2)
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("BalanceCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        // Relationships - one-to-many with cascade delete
        builder.HasMany(e => e.Accommodations)
            .WithOne()
            .HasForeignKey(e => e.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(e => e.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Payments)
            .WithOne()
            .HasForeignKey(e => e.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Concurrency token
        builder.Property(e => e.Version)
            .IsConcurrencyToken();

        // Indexes for common queries
        builder.HasIndex(e => e.ReservationNumber)
            .IsUnique();

        builder.HasIndex(e => e.TenantId);

        builder.HasIndex(e => e.CustomerId);

        builder.HasIndex(e => e.Status);

        builder.HasIndex(e => e.CheckInDate);

        builder.HasIndex(e => e.CheckOutDate);

        builder.HasIndex(e => new { e.TenantId, e.CheckInDate, e.Status });

        builder.HasIndex(e => new { e.TenantId, e.CheckOutDate, e.Status });

        builder.HasIndex(e => new { e.TenantId, e.Status });

        builder.HasIndex(e => e.IsDeleted);

        // Composite index for availability checks
        builder.HasIndex(e => new { e.TenantId, e.CheckInDate, e.CheckOutDate, e.Status, e.IsDeleted })
            .HasDatabaseName("IX_Reservations_Availability");
    }
}
