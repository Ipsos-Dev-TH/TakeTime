using Microsoft.EntityFrameworkCore;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Domain.Entities;

namespace TakeTime.GuestExperience.Infrastructure.Repositories;

/// <summary>
/// Implementation of <see cref="IGuestExperienceRepository"/> providing data access
/// for housekeeping tasks, room service orders, and maintenance requests.
/// Maps between application-layer entry models and domain entities persisted via EF Core.
/// </summary>
public class GuestExperienceRepository : IGuestExperienceRepository
{
    private readonly GuestExperienceDbContext _dbContext;

    public GuestExperienceRepository(GuestExperienceDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    #region Housekeeping

    /// <inheritdoc />
    public async Task<Guid> CreateHousekeepingTaskAsync(HousekeepingTaskEntry task, CancellationToken cancellationToken = default)
    {
        var entity = MapToHousekeepingEntity(task);

        _dbContext.HousekeepingTasks.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    /// <inheritdoc />
    public async Task UpdateHousekeepingTaskAsync(HousekeepingTaskEntry task, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.HousekeepingTasks
            .FirstOrDefaultAsync(e => e.Id == task.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Housekeeping task with ID {task.Id} not found.");

        entity.RoomNumber = task.RoomNumber;
        entity.AccommodationId = task.AccommodationId ?? entity.AccommodationId;
        entity.TaskType = task.TaskType;
        entity.Priority = task.Priority;
        entity.Status = task.Status;
        entity.AssignedTo = TryParseGuid(task.AssignedTo);
        entity.AssignedToName = task.AssignedToName;
        entity.Notes = task.Notes ?? task.Description;
        entity.ScheduledAt = task.ScheduledDate;
        entity.StartedAt = task.StartedAt;
        entity.CompletedAt = task.CompletedAt;
        entity.UpdatedAt = DateTime.UtcNow;

        if (task.CompletedBy != null)
        {
            entity.VerifiedBy = task.CompletedBy;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HousekeepingTaskEntry?> GetHousekeepingTaskByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.HousekeepingTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        return entity is null ? null : MapToHousekeepingEntry(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HousekeepingTaskEntry>> GetHousekeepingTasksAsync(
        string tenantId, DateTime? date, string? status, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.HousekeepingTasks
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId);

        if (date.HasValue)
        {
            var targetDate = date.Value.Date;
            query = query.Where(e => e.ScheduledAt.HasValue && e.ScheduledAt.Value.Date == targetDate);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(e => e.Status == status);
        }

        var entities = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToHousekeepingEntry).ToList().AsReadOnly();
    }

    #endregion

    #region Room Service

    /// <inheritdoc />
    public async Task<Guid> CreateRoomServiceOrderAsync(RoomServiceOrderEntry order, CancellationToken cancellationToken = default)
    {
        var entity = MapToRoomServiceEntity(order);

        _dbContext.RoomServiceOrders.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    /// <inheritdoc />
    public async Task UpdateRoomServiceOrderAsync(RoomServiceOrderEntry order, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RoomServiceOrders
            .Include(e => e.Items)
            .FirstOrDefaultAsync(e => e.Id == order.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Room service order with ID {order.Id} not found.");

        entity.OrderNumber = order.OrderNumber;
        entity.RoomNumber = order.RoomNumber;
        entity.ReservationId = order.ReservationId ?? entity.ReservationId;
        entity.Status = order.Status;
        entity.SubTotal = order.SubTotal;
        entity.VATAmount = order.VATAmount;
        entity.TotalAmount = order.TotalAmount;
        entity.Currency = order.Currency;
        entity.ChargeToRoom = order.ChargeToRoom;
        entity.SpecialInstructions = order.SpecialInstructions;
        entity.DeliveredAt = order.DeliveredAt;
        entity.UpdatedAt = DateTime.UtcNow;

        // Sync items: remove existing and add updated items
        _dbContext.RoomServiceOrderItems.RemoveRange(entity.Items);
        entity.Items = order.Items.Select(item => new RoomServiceOrderItem
        {
            Id = item.Id != Guid.Empty ? item.Id : Guid.NewGuid(),
            OrderId = entity.Id,
            ProductName = item.ItemName,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.TotalPrice,
            Notes = item.SpecialRequest
        }).ToList();

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RoomServiceOrderEntry?> GetRoomServiceOrderByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RoomServiceOrders
            .AsNoTracking()
            .Include(e => e.Items)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        return entity is null ? null : MapToRoomServiceEntry(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoomServiceOrderEntry>> GetRoomServiceOrdersAsync(
        string tenantId, string? status, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RoomServiceOrders
            .AsNoTracking()
            .Include(e => e.Items)
            .Where(e => e.TenantId == tenantId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(e => e.Status == status);
        }

        var entities = await query
            .OrderByDescending(e => e.OrderedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToRoomServiceEntry).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<int> GetTodayOrderCountAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        return await _dbContext.RoomServiceOrders
            .CountAsync(e => e.TenantId == tenantId && e.OrderedAt.Date == today, cancellationToken);
    }

    #endregion

    #region Maintenance

    /// <inheritdoc />
    public async Task<Guid> CreateMaintenanceRequestAsync(MaintenanceRequestEntry request, CancellationToken cancellationToken = default)
    {
        var entity = MapToMaintenanceEntity(request);

        _dbContext.MaintenanceRequests.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    /// <inheritdoc />
    public async Task UpdateMaintenanceRequestAsync(MaintenanceRequestEntry request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.MaintenanceRequests
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Maintenance request with ID {request.Id} not found.");

        entity.RequestNumber = request.RequestNumber;
        entity.RoomNumber = request.RoomNumber;
        entity.AccommodationId = request.AccommodationId ?? entity.AccommodationId;
        entity.IssueType = request.Category;
        entity.Priority = request.Priority;
        entity.Status = request.Status;
        entity.Description = request.Description;
        entity.AssignedTo = TryParseGuid(request.AssignedTo);
        entity.AssignedToName = request.AssignedToName;
        entity.EstimatedCost = request.EstimatedCost;
        entity.ActualCost = request.ActualCost;
        entity.ResolutionNotes = request.ResolutionNotes;
        entity.UpdatedAt = DateTime.UtcNow;

        if (request.CompletedAt.HasValue)
        {
            entity.ResolvedAt = request.CompletedAt;
        }

        if (request.StartedAt.HasValue)
        {
            // Track that work has started (stored in entity's audit fields)
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MaintenanceRequestEntry?> GetMaintenanceRequestByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.MaintenanceRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        return entity is null ? null : MapToMaintenanceEntry(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MaintenanceRequestEntry>> GetMaintenanceRequestsAsync(
        string tenantId, string? status, string? priority, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MaintenanceRequests
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(e => e.Status == status);
        }

        if (!string.IsNullOrEmpty(priority))
        {
            query = query.Where(e => e.Priority == priority);
        }

        var entities = await query
            .OrderByDescending(e => e.ReportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToMaintenanceEntry).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<int> GetTodayMaintenanceCountAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        return await _dbContext.MaintenanceRequests
            .CountAsync(e => e.TenantId == tenantId && e.ReportedAt.Date == today, cancellationToken);
    }

    #endregion

    #region Mapping Helpers

    private static HousekeepingTask MapToHousekeepingEntity(HousekeepingTaskEntry entry)
    {
        return new HousekeepingTask
        {
            RoomNumber = entry.RoomNumber,
            AccommodationId = entry.AccommodationId ?? Guid.Empty,
            TaskType = entry.TaskType,
            Priority = entry.Priority,
            Status = entry.Status,
            AssignedTo = TryParseGuid(entry.AssignedTo),
            AssignedToName = entry.AssignedToName,
            Notes = entry.Notes ?? entry.Description,
            ScheduledAt = entry.ScheduledDate,
            StartedAt = entry.StartedAt,
            CompletedAt = entry.CompletedAt,
            TenantId = entry.TenantId
        };
    }

    private static HousekeepingTaskEntry MapToHousekeepingEntry(HousekeepingTask entity)
    {
        return new HousekeepingTaskEntry
        {
            Id = entity.Id,
            TenantId = entity.TenantId ?? string.Empty,
            RoomNumber = entity.RoomNumber,
            AccommodationId = entity.AccommodationId,
            TaskType = entity.TaskType,
            Priority = entity.Priority,
            Status = entity.Status,
            AssignedTo = entity.AssignedTo?.ToString(),
            AssignedToName = entity.AssignedToName,
            Description = entity.Notes,
            Notes = entity.Notes,
            ScheduledDate = entity.ScheduledAt ?? DateTime.MinValue,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            CompletedBy = entity.VerifiedBy,
            CreatedAt = entity.CreatedAt
        };
    }

    private static RoomServiceOrder MapToRoomServiceEntity(RoomServiceOrderEntry entry)
    {
        var entity = new RoomServiceOrder
        {
            OrderNumber = entry.OrderNumber,
            ReservationId = entry.ReservationId ?? Guid.Empty,
            RoomNumber = entry.RoomNumber,
            SubTotal = entry.SubTotal,
            VATAmount = entry.VATAmount,
            TotalAmount = entry.TotalAmount,
            Currency = entry.Currency,
            ChargeToRoom = entry.ChargeToRoom,
            Status = entry.Status,
            OrderedAt = entry.CreatedAt,
            DeliveredAt = entry.DeliveredAt,
            SpecialInstructions = entry.SpecialInstructions,
            TenantId = entry.TenantId
        };

        entity.Items = entry.Items.Select(item => new RoomServiceOrderItem
        {
            Id = item.Id != Guid.Empty ? item.Id : Guid.NewGuid(),
            OrderId = entity.Id,
            ProductName = item.ItemName,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.TotalPrice,
            Notes = item.SpecialRequest
        }).ToList();

        return entity;
    }

    private static RoomServiceOrderEntry MapToRoomServiceEntry(RoomServiceOrder entity)
    {
        return new RoomServiceOrderEntry
        {
            Id = entity.Id,
            TenantId = entity.TenantId ?? string.Empty,
            OrderNumber = entity.OrderNumber,
            RoomNumber = entity.RoomNumber,
            ReservationId = entity.ReservationId,
            Status = entity.Status,
            SubTotal = entity.SubTotal,
            VATAmount = entity.VATAmount,
            TotalAmount = entity.TotalAmount,
            Currency = entity.Currency,
            ChargeToRoom = entity.ChargeToRoom,
            SpecialInstructions = entity.SpecialInstructions,
            DeliveredAt = entity.DeliveredAt,
            CreatedAt = entity.OrderedAt,
            Items = entity.Items.Select(item => new RoomServiceItemEntry
            {
                Id = item.Id,
                ItemName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                SpecialRequest = item.Notes
            }).ToList()
        };
    }

    private static MaintenanceRequest MapToMaintenanceEntity(MaintenanceRequestEntry entry)
    {
        return new MaintenanceRequest
        {
            RequestNumber = entry.RequestNumber,
            AccommodationId = entry.AccommodationId ?? Guid.Empty,
            RoomNumber = entry.RoomNumber,
            IssueType = entry.Category,
            Description = entry.Description,
            Priority = entry.Priority,
            Status = entry.Status,
            AssignedTo = TryParseGuid(entry.AssignedTo),
            AssignedToName = entry.AssignedToName,
            EstimatedCost = entry.EstimatedCost,
            ActualCost = entry.ActualCost,
            ResolutionNotes = entry.ResolutionNotes,
            ReportedAt = entry.CreatedAt,
            ResolvedAt = entry.CompletedAt,
            TenantId = entry.TenantId
        };
    }

    private static MaintenanceRequestEntry MapToMaintenanceEntry(MaintenanceRequest entity)
    {
        return new MaintenanceRequestEntry
        {
            Id = entity.Id,
            TenantId = entity.TenantId ?? string.Empty,
            RequestNumber = entity.RequestNumber,
            RoomNumber = entity.RoomNumber,
            AccommodationId = entity.AccommodationId,
            Category = entity.IssueType,
            Priority = entity.Priority,
            Status = entity.Status,
            Description = entity.Description,
            AssignedTo = entity.AssignedTo?.ToString(),
            AssignedToName = entity.AssignedToName,
            EstimatedCost = entity.EstimatedCost,
            ActualCost = entity.ActualCost,
            ResolutionNotes = entity.ResolutionNotes,
            StartedAt = null,
            CompletedAt = entity.ResolvedAt,
            CreatedAt = entity.ReportedAt
        };
    }

    private static Guid? TryParseGuid(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    #endregion
}
