using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Notification.Domain.Entities;
using TakeTime.Notification.Domain.Enums;
using TakeTime.Infrastructure.Database;

namespace TakeTime.Notification.Infrastructure.Repositories;

public class NotificationDbContext : BaseDbContext
{
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public NotificationDbContext(
        DbContextOptions<NotificationDbContext> options,
        ICurrentTenantService tenantService)
        : base(options, tenantService)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.Channel, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.RelatedEntityType, e.RelatedEntityId });
            entity.HasIndex(e => new { e.TenantId, e.Recipient });
            entity.Property(e => e.Recipient).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.Body).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.RelatedEntityType).HasMaxLength(100);
            entity.Property(e => e.Channel).HasConversion<string>().HasMaxLength(50);
        });

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.TemplateName, e.Channel }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.IsActive });
            entity.Property(e => e.TemplateName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.Body).IsRequired();
            entity.Property(e => e.Language).HasMaxLength(10).HasDefaultValue("th");
            entity.Property(e => e.Channel).HasConversion<string>().HasMaxLength(50);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.RoomNumber, e.IsConversationActive });
            entity.HasIndex(e => new { e.TenantId, e.RoomNumber, e.CreatedAt });
            entity.Property(e => e.RoomNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.SenderType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.SenderName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(4000).IsRequired();
        });
    }
}
