using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.MultiTenancy.Features;

namespace TakeTime.GuestExperience.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[FeatureGate(FeatureModule.GuestExperience, SubFeature = "MaintenanceTracking")]
public class MaintenanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public MaintenanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Report(
        [FromBody] CreateMaintenanceRequestCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Report), result);
    }
}
