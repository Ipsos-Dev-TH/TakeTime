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
}
