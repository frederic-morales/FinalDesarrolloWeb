using CuentasPorPagarAPI.Contracts;
using CuentasPorPagarAPI.Data;
using CuentasPorPagarAPI.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CuentasPorPagarAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/suppliers")]
public sealed class SuppliersController(PayablesDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PageResult<SupplierResponse>>> List(
        [FromQuery] string? search, [FromQuery] bool? active, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Suppliers.AsNoTracking();
        if (active is not null) query = query.Where(x => x.IsActive == active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || (x.Nit != null && x.Nit.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new SupplierResponse(x.Id, x.Name, x.Nit, x.ContactName, x.Email, x.Phone, x.Address, x.IsActive))
            .ToListAsync(cancellationToken);
        return Ok(new PageResult<SupplierResponse>(items, total, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SupplierResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new SupplierResponse(x.Id, x.Name, x.Nit, x.ContactName, x.Email, x.Phone, x.Address, x.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        return supplier is null ? NotFound() : Ok(supplier);
    }

    [HttpPost]
    public async Task<ActionResult<SupplierResponse>> Create(SupplierRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var nit = Clean(request.Nit);
        if (name.Length == 0) return BadRequest("El nombre es obligatorio.");
        if (nit is not null && await db.Suppliers.AnyAsync(x => x.Nit == nit, cancellationToken))
            return Conflict("Ya existe un proveedor con este NIT.");

        var supplier = new Supplier
        {
            Name = name, Nit = nit, ContactName = Clean(request.ContactName),
            Email = Clean(request.Email), Phone = Clean(request.Phone), Address = Clean(request.Address)
        };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = supplier.Id }, ToResponse(supplier));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SupplierResponse>> Update(int id, SupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.FindAsync([id], cancellationToken);
        if (supplier is null) return NotFound();
        var name = request.Name.Trim();
        var nit = Clean(request.Nit);
        if (name.Length == 0) return BadRequest("El nombre es obligatorio.");
        if (nit is not null && await db.Suppliers.AnyAsync(x => x.Nit == nit && x.Id != id, cancellationToken))
            return Conflict("Ya existe un proveedor con este NIT.");

        supplier.Name = name;
        supplier.Nit = nit;
        supplier.ContactName = Clean(request.ContactName);
        supplier.Email = Clean(request.Email);
        supplier.Phone = Clean(request.Phone);
        supplier.Address = Clean(request.Address);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(supplier));
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> SetStatus(int id, SupplierStatusRequest request, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.FindAsync([id], cancellationToken);
        if (supplier is null) return NotFound();
        supplier.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SupplierResponse ToResponse(Supplier x) =>
        new(x.Id, x.Name, x.Nit, x.ContactName, x.Email, x.Phone, x.Address, x.IsActive);
}
