using System.Security.Claims;
using CuentasPorPagarAPI.Application;
using CuentasPorPagarAPI.Contracts;
using CuentasPorPagarAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CuentasPorPagarAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/payments")]
public sealed class PaymentsController(PaymentService payments) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PageResult<PaymentListResponse>>> List(
        [FromQuery] int? supplierId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await payments.ListAsync(supplierId, from, to, page, pageSize, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PaymentResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var payment = await payments.GetAsync(id, cancellationToken);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> Create(PaymentRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var payment = await payments.CreateAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = payment.Id }, payment);
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        await payments.CancelAsync(id, cancellationToken);
        return NoContent();
    }
}
