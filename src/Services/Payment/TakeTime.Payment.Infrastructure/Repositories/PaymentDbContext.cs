using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Infrastructure.Database;
using TakeTime.Payment.Domain.Entities;
using TakeTime.Payment.Domain.ValueObjects;

namespace TakeTime.Payment.Infrastructure.Repositories;

/// <summary>
/// EF Core DbContext for the Payment microservice.
/// Extends BaseDbContext to inherit multi-tenant isolation, soft-delete filtering,
/// automatic audit fields, and domain event dispatching.
/// </summary>
public class PaymentDbContext : BaseDbContext
{
    public DbSet<Domain.Entities.Payment> Payments => Set<Domain.Entities.Payment>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptItem> ReceiptItems => Set<ReceiptItem>();
    public DbSet<TaxInvoice> TaxInvoices => Set<TaxInvoice>();

    public PaymentDbContext(
        DbContextOptions<PaymentDbContext> options,
        ICurrentTenantService currentTenantService,
        IMediator? mediator = null,
        ILogger<PaymentDbContext>? logger = null)
        : base(options, currentTenantService, mediator, logger)
    {
    }

    protected override void ApplyEntityConfigurations(ModelBuilder modelBuilder)
    {
        // Payment entity configuration
        modelBuilder.Entity<Domain.Entities.Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PaymentNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PaymentMethod).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.SlipImageUrl).HasMaxLength(500);
            entity.Property(e => e.VerifiedBy).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedBy).HasMaxLength(200);
            entity.Property(e => e.UpdatedBy).HasMaxLength(200);

            // Money value objects
            entity.OwnsOne(e => e.Amount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("Amount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3);
            });

            entity.OwnsOne(e => e.RefundedAmount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("RefundedAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("RefundedCurrency").HasMaxLength(3);
            });

            // BankTransferDetails as owned type
            entity.OwnsOne(e => e.BankTransferDetails, btd =>
            {
                btd.Property(b => b.BankName).HasColumnName("BT_BankName").HasMaxLength(100);
                btd.Property(b => b.AccountNumber).HasColumnName("BT_AccountNumber").HasMaxLength(50);
                btd.Property(b => b.TransferDate).HasColumnName("BT_TransferDate");
                btd.Property(b => b.TransferTime).HasColumnName("BT_TransferTime");
                btd.Property(b => b.Amount).HasColumnName("BT_Amount").HasPrecision(18, 2);
                btd.Property(b => b.ReferenceNumber).HasColumnName("BT_ReferenceNumber").HasMaxLength(100);
            });

            entity.HasIndex(e => e.PaymentNumber).IsUnique();
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.ReservationId);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.TenantId, e.Status });

            entity.Property(e => e.Version).IsConcurrencyToken();
        });

        // Receipt entity configuration
        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.ToTable("Receipts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReceiptNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ReceiptType).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.IssuedBy).IsRequired().HasMaxLength(200);
            entity.Property(e => e.VoidedBy).HasMaxLength(200);
            entity.Property(e => e.VoidReason).HasMaxLength(500);
            entity.Property(e => e.VATRate).HasPrecision(5, 2);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(100);

            entity.OwnsOne(e => e.SubTotal, money =>
            {
                money.Property(m => m.Amount).HasColumnName("SubTotal").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("SubTotalCurrency").HasMaxLength(3);
            });

            entity.OwnsOne(e => e.VATAmount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("VATAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("VATCurrency").HasMaxLength(3);
            });

            entity.OwnsOne(e => e.TotalAmount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("TotalAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("TotalCurrency").HasMaxLength(3);
            });

            entity.HasMany(e => e.Items)
                .WithOne()
                .HasForeignKey(e => e.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ReceiptNumber).IsUnique();
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.PaymentId);
            entity.HasIndex(e => e.ReservationId);
            entity.HasIndex(e => e.ReceiptType);

            entity.Property(e => e.Version).IsConcurrencyToken();
        });

        // ReceiptItem configuration
        modelBuilder.Entity<ReceiptItem>(entity =>
        {
            entity.ToTable("ReceiptItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.TenantId).HasMaxLength(100);

            entity.OwnsOne(e => e.UnitPrice, money =>
            {
                money.Property(m => m.Amount).HasColumnName("UnitPrice").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(3);
            });

            entity.OwnsOne(e => e.Amount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("ItemAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("ItemCurrency").HasMaxLength(3);
            });

            entity.OwnsOne(e => e.DiscountAmount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("DiscountAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("DiscountCurrency").HasMaxLength(3);
            });

            entity.HasIndex(e => e.ReceiptId);
        });

        // TaxInvoice configuration
        modelBuilder.Entity<TaxInvoice>(entity =>
        {
            entity.ToTable("TaxInvoices");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.VATRate).HasPrecision(5, 2);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.PDFUrl).HasMaxLength(500);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(100);

            entity.OwnsOne(e => e.SellerInfo, ci =>
            {
                ci.Property(c => c.CompanyName).HasColumnName("Seller_CompanyName").HasMaxLength(200);
                ci.Property(c => c.TaxId).HasColumnName("Seller_TaxId").HasMaxLength(50);
                ci.Property(c => c.BranchNumber).HasColumnName("Seller_BranchNumber").HasMaxLength(10);
                ci.Property(c => c.Address).HasColumnName("Seller_Address").HasMaxLength(500);
                ci.Property(c => c.Phone).HasColumnName("Seller_Phone").HasMaxLength(50);
                ci.Property(c => c.Email).HasColumnName("Seller_Email").HasMaxLength(200);
            });

            entity.OwnsOne(e => e.BuyerInfo, ci =>
            {
                ci.Property(c => c.CompanyName).HasColumnName("Buyer_CompanyName").HasMaxLength(200);
                ci.Property(c => c.TaxId).HasColumnName("Buyer_TaxId").HasMaxLength(50);
                ci.Property(c => c.BranchNumber).HasColumnName("Buyer_BranchNumber").HasMaxLength(10);
                ci.Property(c => c.Address).HasColumnName("Buyer_Address").HasMaxLength(500);
                ci.Property(c => c.Phone).HasColumnName("Buyer_Phone").HasMaxLength(50);
                ci.Property(c => c.Email).HasColumnName("Buyer_Email").HasMaxLength(200);
            });

            entity.OwnsOne(e => e.SubTotal, money =>
            {
                money.Property(m => m.Amount).HasColumnName("SubTotal").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("SubTotalCurrency").HasMaxLength(3);
            });

            entity.OwnsOne(e => e.VATAmount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("VATAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("VATCurrency").HasMaxLength(3);
            });

            entity.OwnsOne(e => e.TotalAmount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("TotalAmount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("TotalCurrency").HasMaxLength(3);
            });

            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.ReceiptId);

            entity.Property(e => e.Version).IsConcurrencyToken();
        });
    }
}
