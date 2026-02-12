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
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();
    public DbSet<SubscriptionInvoice> SubscriptionInvoices => Set<SubscriptionInvoice>();

    public TenantDbContext(DbContextOptions<TenantDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new TenantEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionPlanEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TenantSubscriptionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionPaymentEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionInvoiceEntityConfiguration());
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

/// <summary>
/// EF Core entity configuration for the SubscriptionPlan entity.
/// </summary>
internal class SubscriptionPlanEntityConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("SubscriptionPlans", "multitenancy");

        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.Code)
            .IsUnique()
            .HasDatabaseName("IX_SubscriptionPlans_Code");

        builder.HasIndex(p => p.Tier)
            .HasDatabaseName("IX_SubscriptionPlans_Tier");

        builder.HasIndex(p => p.IsActive)
            .HasDatabaseName("IX_SubscriptionPlans_IsActive");

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.NameTh)
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.DescriptionTh)
            .HasMaxLength(2000);

        builder.Property(p => p.Tier)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(p => p.MonthlyPrice)
            .HasPrecision(18, 2);

        builder.Property(p => p.YearlyPrice)
            .HasPrecision(18, 2);

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("THB");

        // JSON-serialized list properties
        builder.Property(p => p.IncludedFeatures)
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>())
            .Metadata.SetValueComparer(CreateStringListComparer());

        builder.Property(p => p.IncludedModules)
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>())
            .Metadata.SetValueComparer(CreateStringListComparer());
    }

    private static ValueComparer<List<string>> CreateStringListComparer()
    {
        return new ValueComparer<List<string>>(
            (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
            v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
            v => JsonSerializer.Deserialize<List<string>>(
                JsonSerializer.Serialize(v, JsonOptions), JsonOptions)!);
    }
}

/// <summary>
/// EF Core entity configuration for the TenantSubscription entity.
/// </summary>
internal class TenantSubscriptionEntityConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("TenantSubscriptions", "multitenancy");

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("IX_TenantSubscriptions_TenantId");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("IX_TenantSubscriptions_Status");

        builder.HasIndex(s => new { s.TenantId, s.Status })
            .HasDatabaseName("IX_TenantSubscriptions_TenantId_Status");

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.BillingCycle)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.PriceAtSubscription)
            .HasPrecision(18, 2);

        builder.Property(s => s.DiscountPercentage)
            .HasPrecision(5, 2);

        builder.Property(s => s.FinalPrice)
            .HasPrecision(18, 2);

        builder.Property(s => s.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("THB");

        builder.Property(s => s.DiscountCode)
            .HasMaxLength(50);

        builder.Property(s => s.CancellationReason)
            .HasMaxLength(1000);

        // Navigation to SubscriptionPlan
        builder.HasOne(s => s.Plan)
            .WithMany()
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// EF Core entity configuration for the SubscriptionPayment entity.
/// </summary>
internal class SubscriptionPaymentEntityConfiguration : IEntityTypeConfiguration<SubscriptionPayment>
{
    public void Configure(EntityTypeBuilder<SubscriptionPayment> builder)
    {
        builder.ToTable("SubscriptionPayments", "multitenancy");

        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.TenantId)
            .HasDatabaseName("IX_SubscriptionPayments_TenantId");

        builder.HasIndex(p => p.SubscriptionId)
            .HasDatabaseName("IX_SubscriptionPayments_SubscriptionId");

        builder.HasIndex(p => p.PaymentNumber)
            .IsUnique()
            .HasDatabaseName("IX_SubscriptionPayments_PaymentNumber");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("IX_SubscriptionPayments_Status");

        builder.Property(p => p.PaymentNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Amount)
            .HasPrecision(18, 2);

        builder.Property(p => p.VatAmount)
            .HasPrecision(18, 2);

        builder.Property(p => p.TotalAmount)
            .HasPrecision(18, 2);

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("THB");

        builder.Property(p => p.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(p => p.BankName)
            .HasMaxLength(200);

        builder.Property(p => p.TransferReference)
            .HasMaxLength(200);

        builder.Property(p => p.SlipImageUrl)
            .HasMaxLength(1000);

        builder.Property(p => p.CardLast4)
            .HasMaxLength(4);

        builder.Property(p => p.CardBrand)
            .HasMaxLength(50);

        builder.Property(p => p.GatewayTransactionId)
            .HasMaxLength(200);

        builder.Property(p => p.InvoiceNumber)
            .HasMaxLength(50);

        // Navigation to TenantSubscription
        builder.HasOne(p => p.Subscription)
            .WithMany()
            .HasForeignKey(p => p.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// EF Core entity configuration for the SubscriptionInvoice entity.
/// </summary>
internal class SubscriptionInvoiceEntityConfiguration : IEntityTypeConfiguration<SubscriptionInvoice>
{
    public void Configure(EntityTypeBuilder<SubscriptionInvoice> builder)
    {
        builder.ToTable("SubscriptionInvoices", "multitenancy");

        builder.HasKey(i => i.Id);

        builder.HasIndex(i => i.TenantId)
            .HasDatabaseName("IX_SubscriptionInvoices_TenantId");

        builder.HasIndex(i => i.SubscriptionId)
            .HasDatabaseName("IX_SubscriptionInvoices_SubscriptionId");

        builder.HasIndex(i => i.InvoiceNumber)
            .IsUnique()
            .HasDatabaseName("IX_SubscriptionInvoices_InvoiceNumber");

        builder.HasIndex(i => i.Status)
            .HasDatabaseName("IX_SubscriptionInvoices_Status");

        builder.Property(i => i.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.PlanName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.BillingCycleDescription)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.SubTotal)
            .HasPrecision(18, 2);

        builder.Property(i => i.DiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(i => i.VatRate)
            .HasPrecision(5, 2)
            .HasDefaultValue(7m);

        builder.Property(i => i.VatAmount)
            .HasPrecision(18, 2);

        builder.Property(i => i.TotalAmount)
            .HasPrecision(18, 2);

        builder.Property(i => i.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("THB");

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(i => i.SellerName)
            .HasMaxLength(500);

        builder.Property(i => i.SellerTaxId)
            .HasMaxLength(50);

        builder.Property(i => i.SellerAddress)
            .HasMaxLength(1000);

        builder.Property(i => i.BuyerName)
            .HasMaxLength(500);

        builder.Property(i => i.BuyerTaxId)
            .HasMaxLength(50);

        builder.Property(i => i.BuyerAddress)
            .HasMaxLength(1000);
    }
}
