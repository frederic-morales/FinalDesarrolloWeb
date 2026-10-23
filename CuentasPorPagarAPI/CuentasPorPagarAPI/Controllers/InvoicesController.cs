using CuentasPorPagarAPI.Application;
using CuentasPorPagarAPI.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CuentasPorPagarAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/invoices")]
public sealed class InvoicesController(InvoiceService invoices) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PageResult<InvoiceListResponse>>> List(
        [FromQuery] int? supplierId, [FromQuery] bool? pending, [FromQuery] bool? overdue,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await invoices.ListAsync(supplierId, pending, overdue, page, pageSize, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InvoiceResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetAsync(id, cancellationToken);
        return invoice is null ? NotFound() : Ok(invoice);
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceResponse>> Create(InvoiceRequest request, CancellationToken cancellationToken)
    {
        var invoice = await invoices.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = invoice.Id }, invoice);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<InvoiceResponse>> Update(int id, InvoiceRequest request, CancellationToken cancellationToken) =>
        Ok(await invoices.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        await invoices.CancelAsync(id, cancellationToken);
        return NoContent();
    }
}
