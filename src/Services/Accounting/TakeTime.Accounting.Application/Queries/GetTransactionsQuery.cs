using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Accounting.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Accounting.Application.Queries;

/// <summary>
/// Query to list financial transactions with filters and pagination.
/// </summary>
public sealed class GetTransactionsQuery : IRequest<TransactionListDto>
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Category { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Handler for <see cref="GetTransactionsQuery"/>. Retrieves financial transactions
/// from the repository with filtering and pagination.
/// </summary>
public sealed class GetTransactionsQueryHandler : IRequestHandler<GetTransactionsQuery, TransactionListDto>
{
    private readonly IAccountingQueryRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetTransactionsQueryHandler> _logger;

    public GetTransactionsQueryHandler(
        IAccountingQueryRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetTransactionsQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<TransactionListDto> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Retrieving transactions for tenant {TenantId}. StartDate: {StartDate}, EndDate: {EndDate}, Category: {Category}, Page: {Page}",
            tenantId, request.StartDate, request.EndDate, request.Category, request.Page);

        var (transactions, totalCount) = await _repository.GetTransactionsPagedAsync(
            tenantId, request.StartDate, request.EndDate, request.Category,
            request.Page, request.PageSize, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        var items = transactions.Select(t => new TransactionItemDto
        {
            Id = t.Id,
            Category = t.Category,
            SubCategory = t.SubCategory,
            Amount = t.Amount,
            TransactionDate = t.TransactionDate,
            Reference = t.Reference,
            Currency = currency,
            CreatedAt = t.TransactionDate
        }).ToList();

        _logger.LogInformation("Retrieved {Count} of {Total} transactions", items.Count, totalCount);

        return new TransactionListDto
        {
            Transactions = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            Currency = currency
        };
    }
}
