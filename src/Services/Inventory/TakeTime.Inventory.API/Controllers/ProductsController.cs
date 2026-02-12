using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Inventory.Application.Commands;
using TakeTime.Inventory.Application.DTOs;
using TakeTime.Inventory.Application.Queries;

namespace TakeTime.Inventory.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> Search(
        [FromQuery] string? keyword, [FromQuery] string? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductsQuery
        {
            Keyword = keyword, Category = category, Page = page, PageSize = pageSize
        }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/adjust-stock")]
    public async Task<IActionResult> AdjustStock(
        Guid id, [FromBody] AdjustStockCommand command, CancellationToken ct)
    {
        command.ProductId = id;
        await _mediator.Send(command, ct);
        return Ok();
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<List<ProductDto>>> GetLowStock(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductsQuery { LowStockOnly = true }, ct);
        return Ok(result);
    }

    /// <summary>Gets a product by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductByIdQuery { ProductId = id }, ct);
        return Ok(result);
    }

    /// <summary>Creates a new product.</summary>
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] CreateProductCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Updates a product.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(
        Guid id, [FromBody] UpdateProductCommand command, CancellationToken ct)
    {
        command.ProductId = id;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Deletes a product.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteProductCommand { ProductId = id }, ct);
        return NoContent();
    }

    /// <summary>Gets stock movements/history for a product.</summary>
    [HttpGet("{id:guid}/stock-history")]
    public async Task<ActionResult<List<StockMovementDto>>> GetStockHistory(
        Guid id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStockHistoryQuery
        {
            ProductId = id,
            FromDate = from,
            ToDate = to
        }, ct);
        return Ok(result);
    }

    /// <summary>Gets product categories.</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<List<CategorySummaryDto>>> GetCategories(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductCategoriesQuery(), ct);
        return Ok(result);
    }
}
