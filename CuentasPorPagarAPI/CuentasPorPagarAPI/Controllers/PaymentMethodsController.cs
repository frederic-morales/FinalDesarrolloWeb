using CuentasPorPagarAPI.Contracts;
using CuentasPorPagarAPI.Data;
using CuentasPorPagarAPI.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CuentasPorPagarAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/payment-methods")]
public sealed class PaymentMethodsController(PayablesDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentMethodResponse>>> List(
        [FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var query = db.PaymentMethods.AsNoTracking();
        if (activeOnly) query = query.Where(x => x.IsActive);
        return Ok(await query.OrderBy(x => x.Name)
            .Select(x => new PaymentMethodResponse(x.Id, x.Name, x.IsActive))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PaymentMethodResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var method = await db.PaymentMethods.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new PaymentMethodResponse(x.Id, x.Name, x.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        return method is null ? NotFound() : Ok(method);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administrator)]
    public async Task<ActionResult<PaymentMethodResponse>> Create(PaymentMethodRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0) return BadRequest("El nombre es obligatorio.");
        if (await db.PaymentMethods.AnyAsync(x => x.Name == name, cancellationToken)) return Conflict("El método ya existe.");
        var method = new PaymentMethod { Name = name };
        db.PaymentMethods.Add(method);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = method.Id }, new PaymentMethodResponse(method.Id, method.Name, method.IsActive));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administrator)]
    public async Task<ActionResult<PaymentMethodResponse>> Update(int id, PaymentMethodRequest request, CancellationToken cancellationToken)
    {
        var method = await db.PaymentMethods.FindAsync([id], cancellationToken);
        if (method is null) return NotFound();
        var name = request.Name.Trim();
        if (name.Length == 0) return BadRequest("El nombre es obligatorio.");
        if (await db.PaymentMethods.AnyAsync(x => x.Id != id && x.Name == name, cancellationToken)) return Conflict("El método ya existe.");
        method.Name = name;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new PaymentMethodResponse(method.Id, method.Name, method.IsActive));
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> SetStatus(int id, PaymentMethodStatusRequest request, CancellationToken cancellationToken)
    {
        var method = await db.PaymentMethods.FindAsync([id], cancellationToken);
        if (method is null) return NotFound();
        method.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
