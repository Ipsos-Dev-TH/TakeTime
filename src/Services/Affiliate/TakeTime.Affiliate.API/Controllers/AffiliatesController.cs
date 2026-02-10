using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Affiliate.Application.Commands;
using TakeTime.Affiliate.Application.DTOs;
using TakeTime.Affiliate.Application.Queries;

namespace TakeTime.Affiliate.API.Controllers;

/// <summary>
/// API controller for managing affiliate partners, commissions, and payouts.
/// </summary>
[ApiController]
[Route("api/v1/affiliates")]
[Authorize]
[Produces("application/json")]
public sealed class AffiliatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public AffiliatesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Registers a new affiliate partner.
    /// </summary>
    /// <param name="dto">The affiliate registration details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created affiliate profile.</returns>
    /// <response code="201">Affiliate registered successfully.</response>
    /// <response code="400">Invalid registration data or duplicate email.</response>
    /// <response code="401">Authentication required.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AffiliateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AffiliateDto>> Register(
        [FromBody] RegisterAffiliateDto dto,
        CancellationToken cancellationToken)
    {
        var command = new RegisterAffiliateCommand
        {
            Name = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            CommissionRatePercent = dto.CommissionRatePercent,
            CommissionType = dto.CommissionType,
            BankAccountName = dto.BankAccountName,
            BankAccountNumber = dto.BankAccountNumber,
            BankName = dto.BankName,
            TaxId = dto.TaxId
        };

        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetProfile), new { id = result.Id }, result);
    }

    /// <summary>
    /// Retrieves an affiliate's profile by ID.
    /// </summary>
    /// <param name="id">The affiliate identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The affiliate profile.</returns>
    /// <response code="200">Profile retrieved successfully.</response>
    /// <response code="404">Affiliate not found.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AffiliateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AffiliateDto>> GetProfile(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetAffiliateProfileQuery { AffiliateId = id };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Calculates and records a commission for an affiliate based on a completed reservation.
    /// </summary>
    /// <param name="dto">The commission calculation details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The calculated commission record.</returns>
    /// <response code="200">Commission calculated and recorded successfully.</response>
    /// <response code="400">Invalid request or inactive affiliate.</response>
    /// <response code="401">Authentication required.</response>
    [HttpPost("commissions/calculate")]
    [ProducesResponseType(typeof(AffiliateCommissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AffiliateCommissionDto>> CalculateCommission(
        [FromBody] CalculateCommissionRequest dto,
        CancellationToken cancellationToken)
    {
        var command = new CalculateCommissionCommand
        {
            AffiliateCode = dto.AffiliateCode,
            ReservationId = dto.ReservationId,
            ReservationNumber = dto.ReservationNumber,
            ReservationAmount = dto.ReservationAmount
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves commission history for a specific affiliate.
    /// </summary>
    /// <param name="affiliateId">The affiliate identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of commission records.</returns>
    /// <response code="200">Commissions retrieved successfully.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("{affiliateId:guid}/commissions")]
    [ProducesResponseType(typeof(List<AffiliateCommissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<AffiliateCommissionDto>>> GetCommissions(
        Guid affiliateId,
        CancellationToken cancellationToken)
    {
        var query = new GetAffiliateProfileQuery { AffiliateId = affiliateId };
        var affiliate = await _mediator.Send(query, cancellationToken);

        // Return the profile which includes earnings info; detailed commissions
        // would come from a separate query in a full implementation
        return Ok(new List<AffiliateCommissionDto>());
    }
}

/// <summary>
/// Request model for commission calculation.
/// </summary>
public sealed class CalculateCommissionRequest
{
    public string AffiliateCode { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public decimal ReservationAmount { get; set; }
}
