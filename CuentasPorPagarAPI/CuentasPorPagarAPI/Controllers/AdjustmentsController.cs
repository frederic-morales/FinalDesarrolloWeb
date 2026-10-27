using System.Security.Claims;
using CuentasPorPagarAPI.Application;
using CuentasPorPagarAPI.Contracts;
using CuentasPorPagarAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CuentasPorPagarAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/adjustments")]
public sealed class AdjustmentsController(AdjustmentService adjustments) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PageResult<AdjustmentListResponse>>> List(
        [FromQuery] int? supplierId, [FromQuery] AdjustmentKind? kind,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await adjustments.ListAsync(supplierId, kind, from, to, page, pageSize, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdjustmentResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var adjustment = await adjustments.GetAsync(id, cancellationToken);
        return adjustment is null ? NotFound() : Ok(adjustment);
    }

    [HttpPost]
    public async Task<ActionResult<AdjustmentResponse>> Create(
        AdjustmentRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var adjustment = await adjustments.CreateAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = adjustment.Id }, adjustment);
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        await adjustments.CancelAsync(id, cancellationToken);
        return NoContent();
    }
}
