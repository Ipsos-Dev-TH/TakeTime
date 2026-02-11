using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;
using TakeTime.MultiTenancy.Features;

namespace TakeTime.GuestExperience.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[FeatureGate(FeatureModule.GuestExperience, SubFeature = "Housekeeping")]
public class HousekeepingController : ControllerBase
{
    private readonly IMediator _mediator;

    public HousekeepingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateHousekeepingTaskCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Create), result);
    }
}
