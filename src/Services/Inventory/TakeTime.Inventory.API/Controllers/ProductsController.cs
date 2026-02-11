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
    public IActionResult GetById(Guid id)
    {
        // TODO: Implement GetProductByIdQuery
        return StatusCode(501, new { message = "Get product by ID not yet implemented." });
    }

    /// <summary>Creates a new product.</summary>
    [HttpPost]
    public IActionResult Create([FromBody] object command)
    {
        // TODO: Implement CreateProductCommand
        return StatusCode(501, new { message = "Create product not yet implemented." });
    }

    /// <summary>Updates a product.</summary>
    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id, [FromBody] object command)
    {
        // TODO: Implement UpdateProductCommand
        return StatusCode(501, new { message = "Update product not yet implemented." });
    }

    /// <summary>Deletes a product.</summary>
    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        // TODO: Implement DeleteProductCommand
        return StatusCode(501, new { message = "Delete product not yet implemented." });
    }

    /// <summary>Gets stock movements/history for a product.</summary>
    [HttpGet("{id:guid}/stock-history")]
    public IActionResult GetStockHistory(Guid id, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        // TODO: Implement GetStockHistoryQuery
        return StatusCode(501, new { message = "Stock history not yet implemented." });
    }

    /// <summary>Gets product categories.</summary>
    [HttpGet("categories")]
    public IActionResult GetCategories()
    {
        // TODO: Implement GetProductCategoriesQuery
        return StatusCode(501, new { message = "Get categories not yet implemented." });
    }
}
