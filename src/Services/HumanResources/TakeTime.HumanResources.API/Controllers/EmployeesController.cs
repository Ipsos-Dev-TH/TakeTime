using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.HumanResources.Application.DTOs;
using TakeTime.HumanResources.Application.Queries;

namespace TakeTime.HumanResources.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<EmployeeDto>>> Search(
        [FromQuery] string? keyword, [FromQuery] string? department,
        [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetEmployeesQuery
        {
            Keyword = keyword, Department = department, Status = status,
            Page = page, PageSize = pageSize
        }, ct);
        return Ok(result);
    }
}
