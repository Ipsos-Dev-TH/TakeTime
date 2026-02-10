using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TakeTime.MultiTenancy.Core;

namespace TakeTime.MultiTenancy.Configuration;

/// <summary>
/// EF Core DbContext for the central tenant management database.
/// This context stores Tenant records and their associated configurations.
/// It is NOT the per-tenant application DbContext -- it is the control-plane
/// database that the multi-tenancy system uses to look up and manage tenants.
/// </summary>
public class TenantDbContext : DbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public TenantDbContext(DbContextOptions<TenantDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new TenantEntityConfiguration());
    }
}

/// <summary>
/// EF Core entity configuration for the Tenant entity.
/// Handles JSON serialization of complex nested objects
/// (BusinessSettings, Metadata) and defines indexes.
/// </summary>
internal class TenantEntityConfiguration : IEntityTypeConfiguration<Tenant>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants", "multitenancy");

        // Primary key
        builder.HasKey(t => t.Id);

        // Unique code index
        builder.HasIndex(t => t.Code)
            .IsUnique()
            .HasDatabaseName("IX_Tenants_Code");

        // Active tenants index for fast lookup
        builder.HasIndex(t => t.IsActive)
            .HasDatabaseName("IX_Tenants_IsActive");

        // Custom domain index (unique, filtered for non-null)
        builder.HasIndex(t => t.CustomDomain)
            .IsUnique()
            .HasFilter("[CustomDomain] IS NOT NULL")
            .HasDatabaseName("IX_Tenants_CustomDomain");

        // Properties
        builder.Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(t => t.DatabaseConnectionString)
            .HasMaxLength(1000);

        builder.Property(t => t.LogoUrl)
            .HasMaxLength(500);

        builder.Property(t => t.FaviconUrl)
            .HasMaxLength(500);

        builder.Property(t => t.PrimaryColor)
            .HasMaxLength(20);

        builder.Property(t => t.SecondaryColor)
            .HasMaxLength(20);

        builder.Property(t => t.CustomDomain)
            .HasMaxLength(250);

        builder.Property(t => t.ContactEmail)
            .HasMaxLength(250);

        builder.Property(t => t.ContactPhone)
            .HasMaxLength(50);

        builder.Property(t => t.WebsiteUrl)
            .HasMaxLength(500);

        builder.Property(t => t.AddressLine1)
            .HasMaxLength(250);

        builder.Property(t => t.AddressLine2)
            .HasMaxLength(250);

        builder.Property(t => t.City)
            .HasMaxLength(100);

        builder.Property(t => t.StateProvince)
            .HasMaxLength(100);

        builder.Property(t => t.PostalCode)
            .HasMaxLength(20);

        builder.Property(t => t.Country)
            .IsRequired()
            .HasMaxLength(5)
            .HasDefaultValue("TH");

        builder.Property(t => t.SubscriptionTier)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.CreatedBy)
            .HasMaxLength(250);

        builder.Property(t => t.UpdatedBy)
            .HasMaxLength(250);

        builder.Property(t => t.Notes)
            .HasMaxLength(2000);

        builder.Property(t => t.MaxRooms)
            .HasDefaultValue(10);

        builder.Property(t => t.MaxUsers)
            .HasDefaultValue(5);

        // ─── JSON-serialized complex properties ──────────────────────

        // BusinessSettings stored as JSON column
        builder.Property(t => t.BusinessSettings)
            .HasColumnName("BusinessSettings")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<TenantBusinessSettings>(v, JsonOptions)
                     ?? new TenantBusinessSettings())
            .Metadata.SetValueComparer(CreateBusinessSettingsComparer());

        // Metadata stored as JSON column
        builder.Property(t => t.Metadata)
            .HasColumnName("Metadata")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonOptions)
                     ?? new Dictionary<string, string>())
            .Metadata.SetValueComparer(CreateDictionaryComparer());
    }

    private static ValueComparer<TenantBusinessSettings> CreateBusinessSettingsComparer()
    {
        return new ValueComparer<TenantBusinessSettings>(
            (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
            v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
            v => JsonSerializer.Deserialize<TenantBusinessSettings>(
                JsonSerializer.Serialize(v, JsonOptions), JsonOptions)!);
    }

    private static ValueComparer<Dictionary<string, string>> CreateDictionaryComparer()
    {
        return new ValueComparer<Dictionary<string, string>>(
            (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
            v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
            v => JsonSerializer.Deserialize<Dictionary<string, string>>(
                JsonSerializer.Serialize(v, JsonOptions), JsonOptions)!);
    }
}
