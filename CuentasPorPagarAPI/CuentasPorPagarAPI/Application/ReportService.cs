using CuentasPorPagarAPI.Contracts;
using CuentasPorPagarAPI.Data;
using CuentasPorPagarAPI.Domain;
using Microsoft.EntityFrameworkCore;

namespace CuentasPorPagarAPI.Application;

public sealed class ReportService(PayablesDbContext db)
{
    public async Task<DashboardResponse> DashboardAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));
        var activeSuppliers = await db.Suppliers.CountAsync(x => x.IsActive, cancellationToken);
        var rows = await BasePayables(today).Select(x => new { x.Invoice.DueDate, x.Invoice.Total, x.Paid })
            .ToListAsync(cancellationToken);
        var pending = rows.Where(x => x.Total > x.Paid).ToList();
        var overdue = pending.Where(x => x.DueDate < today).ToList();
        return new DashboardResponse(activeSuppliers, pending.Count, overdue.Count,
            pending.Sum(x => x.Total - x.Paid), overdue.Sum(x => x.Total - x.Paid));
    }

    public async Task<PageResult<InvoiceListResponse>> PayablesAsync(
        DateOnly asOf, int? supplierId, bool overdueOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (asOf == default) throw new ApiException(400, "La fecha de corte es obligatoria.");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = BasePayables(asOf).Where(x => x.Invoice.Total > x.Paid);
        if (supplierId is not null) query = query.Where(x => x.Invoice.SupplierId == supplierId);
        if (overdueOnly) query = query.Where(x => x.Invoice.DueDate < asOf);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.Invoice.DueDate).ThenBy(x => x.Invoice.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Invoice.Id, x.Invoice.SupplierId, SupplierName = x.Invoice.Supplier.Name,
                x.Invoice.Series, x.Invoice.Number, x.Invoice.IssueDate, x.Invoice.DueDate,
                x.Invoice.Total, x.Paid, x.Invoice.Status
            }).ToListAsync(cancellationToken);
        var items = rows.Select(x => new InvoiceListResponse(x.Id, x.SupplierId, x.SupplierName,
            x.Series, x.Number, x.IssueDate, x.DueDate, x.Total, x.Paid, x.Total - x.Paid,
            x.Status.ToString(), x.DueDate < asOf)).ToList();
        return new PageResult<InvoiceListResponse>(items, total, page, pageSize);
    }

    public async Task<StatementResponse> StatementAsync(
        int supplierId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (from == default || to == default || from > to)
            throw new ApiException(400, "El rango de fechas es inválido.");
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == supplierId, cancellationToken)
            ?? throw new ApiException(404, "Proveedor no encontrado.");

        var invoices = await db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.SupplierId == supplierId && x.Status == DocumentStatus.Active && x.IssueDate <= to)
            .Select(x => new { x.Id, x.IssueDate, x.Series, x.Number, x.Total })
            .ToListAsync(cancellationToken);
        var payments = await db.Payments.AsNoTracking()
            .Where(x => x.SupplierId == supplierId && x.Status == DocumentStatus.Active && x.Date <= to)
            .Select(x => new { x.Id, x.Date, x.Reference, x.Amount })
            .ToListAsync(cancellationToken);

        var entries = invoices.Select(x => new Entry(x.IssueDate, "Factura", x.Id,
                $"Factura {x.Series}-{x.Number}", x.Total, 0m))
            .Concat(payments.Select(x => new Entry(x.Date, "Pago", x.Id,
                string.IsNullOrWhiteSpace(x.Reference) ? $"Pago #{x.Id}" : $"Pago {x.Reference}", 0m, x.Amount)))
            .OrderBy(x => x.Date).ThenBy(x => x.Type == "Factura" ? 0 : 1).ThenBy(x => x.DocumentId)
            .ToList();

        var opening = entries.Where(x => x.Date < from).Sum(x => x.Charge - x.Credit);
        var balance = opening;
        var movements = new List<StatementMovement>();
        foreach (var entry in entries.Where(x => x.Date >= from))
        {
            balance += entry.Charge - entry.Credit;
            movements.Add(new StatementMovement(entry.Date, entry.Type, entry.DocumentId,
                entry.Description, entry.Charge, entry.Credit, balance));
        }
        return new StatementResponse(supplier.Id, supplier.Name, from, to, opening, balance, movements);
    }

    private IQueryable<PayableRow> BasePayables(DateOnly asOf) =>
        db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.Status == DocumentStatus.Active && x.IssueDate <= asOf)
            .Select(x => new PayableRow
            {
                Invoice = x,
                Paid = x.Allocations.Where(a => a.Payment.Status == DocumentStatus.Active && a.Payment.Date <= asOf)
                    .Sum(a => (decimal?)a.Amount) ?? 0m
            });

    private sealed class PayableRow
    {
        public PurchaseInvoice Invoice { get; init; } = null!;
        public decimal Paid { get; init; }
    }

    private sealed record Entry(DateOnly Date, string Type, int DocumentId, string Description,
        decimal Charge, decimal Credit);
}
