using CuentasPorPagarAPI.Contracts;
using CuentasPorPagarAPI.Contexts;
using CuentasPorPagarAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CuentasPorPagarAPI.Application;

public sealed class ReportService(PayablesDbContext db)
{
    public async Task<DashboardResponse> DashboardAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));
        var activeSuppliers = await db.Suppliers.CountAsync(x => x.IsActive, cancellationToken);
        var rows = await PayableQuery.At(db, today).Select(x => new
            { x.Invoice.DueDate, x.Invoice.Total, x.Paid, x.Debit, x.Credit })
            .ToListAsync(cancellationToken);
        var pending = rows.Where(x => x.Total + x.Debit > x.Paid + x.Credit).ToList();
        var overdue = pending.Where(x => x.DueDate < today).ToList();
        return new DashboardResponse(activeSuppliers, pending.Count, overdue.Count,
            pending.Sum(x => x.Total + x.Debit - x.Credit - x.Paid),
            overdue.Sum(x => x.Total + x.Debit - x.Credit - x.Paid));
    }

    public async Task<PageResult<InvoiceListResponse>> PayablesAsync(
        DateOnly asOf, int? supplierId, bool overdueOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (asOf == default) throw new ApiException(400, "La fecha de corte es obligatoria.");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = PayableQuery.At(db, asOf)
            .Where(x => x.Invoice.Total + x.Debit > x.Paid + x.Credit);
        if (supplierId is not null) query = query.Where(x => x.Invoice.SupplierId == supplierId);
        if (overdueOnly) query = query.Where(x => x.Invoice.DueDate < asOf);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.Invoice.DueDate).ThenBy(x => x.Invoice.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Invoice.Id, x.Invoice.SupplierId, SupplierName = x.Invoice.Supplier.Name,
                x.Invoice.Series, x.Invoice.Number, x.Invoice.IssueDate, x.Invoice.DueDate,
                x.Invoice.Total, x.Paid, x.Debit, x.Credit, x.Invoice.Status
            }).ToListAsync(cancellationToken);
        var items = rows.Select(x => new InvoiceListResponse(x.Id, x.SupplierId, x.SupplierName,
            x.Series, x.Number, x.IssueDate, x.DueDate, x.Total, x.Paid,
            x.Total + x.Debit - x.Credit - x.Paid,
            x.Status.ToString(), x.DueDate < asOf, x.Debit, x.Credit)).ToList();
        return new PageResult<InvoiceListResponse>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<SupplierBalanceResponse>> SupplierBalancesAsync(
        DateOnly asOf, CancellationToken cancellationToken)
    {
        if (asOf == default) throw new ApiException(400, "La fecha de corte es obligatoria.");
        var rows = await PayableQuery.At(db, asOf)
            .Select(x => new
            {
                x.Invoice.SupplierId, SupplierName = x.Invoice.Supplier.Name,
                x.Invoice.DueDate, x.Invoice.Total, x.Paid, x.Debit, x.Credit
            }).ToListAsync(cancellationToken);
        var balances = rows.GroupBy(x => new { x.SupplierId, x.SupplierName })
            .Select(group => new SupplierBalanceResponse(group.Key.SupplierId, group.Key.SupplierName,
                asOf, group.Count(x => x.Total + x.Debit > x.Paid + x.Credit),
                group.Sum(x => Math.Max(0m, x.Total + x.Debit - x.Credit - x.Paid)),
                group.Where(x => x.DueDate < asOf)
                    .Sum(x => Math.Max(0m, x.Total + x.Debit - x.Credit - x.Paid))))
            .ToDictionary(x => x.SupplierId);
        var suppliers = await db.Suppliers.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name }).ToListAsync(cancellationToken);
        return suppliers.Select(x => balances.GetValueOrDefault(x.Id) ??
            new SupplierBalanceResponse(x.Id, x.Name, asOf, 0, 0m, 0m)).ToList();
    }

    public async Task<StatementResponse> StatementAsync(
        int supplierId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (from == default || to == default || from > to)
            throw new ApiException(400, "El rango de fechas es inválido.");
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == supplierId, cancellationToken)
            ?? throw new ApiException(404, "Proveedor no encontrado.");

        var invoices = await db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.SupplierId == supplierId && x.IssueDate <= to &&
                (x.Status == DocumentStatus.Active || x.CancelledOn != null))
            .Select(x => new { x.Id, x.IssueDate, x.Series, x.Number, x.Total, x.CancelledOn })
            .ToListAsync(cancellationToken);
        var payments = await db.Payments.AsNoTracking()
            .Where(x => x.SupplierId == supplierId && x.Date <= to &&
                (x.Status == DocumentStatus.Active || x.CancelledOn != null))
            .Select(x => new { x.Id, x.Date, x.Reference, x.Amount, x.CancelledOn,
                IsCheck = x.CheckDetail != null })
            .ToListAsync(cancellationToken);
        var adjustments = await db.SupplierAdjustments.AsNoTracking()
            .Where(x => x.SupplierId == supplierId && x.Date <= to &&
                (x.Status == DocumentStatus.Active || x.CancelledOn != null))
            .Select(x => new { x.Id, x.Date, x.Kind, x.Series, x.Number, x.Amount, x.CancelledOn })
            .ToListAsync(cancellationToken);

        var entries = new List<Entry>();
        foreach (var invoice in invoices)
        {
            entries.Add(new Entry(invoice.IssueDate, "Factura", invoice.Id,
                $"Factura {invoice.Series}-{invoice.Number}", invoice.Total, 0m));
            if (invoice.CancelledOn <= to)
                entries.Add(new Entry(invoice.CancelledOn!.Value, "Anulación factura", invoice.Id,
                    $"Factura {invoice.Series}-{invoice.Number}", 0m, invoice.Total));
        }
        foreach (var payment in payments)
        {
            var description = string.IsNullOrWhiteSpace(payment.Reference)
                ? $"Pago #{payment.Id}" : $"Pago {payment.Reference}";
            entries.Add(new Entry(payment.Date, payment.IsCheck ? "Cheque" : "Pago",
                payment.Id, description, 0m, payment.Amount));
            if (payment.CancelledOn <= to)
                entries.Add(new Entry(payment.CancelledOn!.Value, "Anulación pago", payment.Id,
                    description, payment.Amount, 0m));
        }
        foreach (var adjustment in adjustments)
        {
            var charge = adjustment.Kind is AdjustmentKind.DebitNote or AdjustmentKind.OtherCharge;
            var type = AdjustmentLabel(adjustment.Kind);
            var description = $"{type} {adjustment.Series}-{adjustment.Number}";
            entries.Add(new Entry(adjustment.Date, type, adjustment.Id, description,
                charge ? adjustment.Amount : 0m, charge ? 0m : adjustment.Amount));
            if (adjustment.CancelledOn <= to)
                entries.Add(new Entry(adjustment.CancelledOn!.Value, $"Anulación {type.ToLowerInvariant()}",
                    adjustment.Id, description, charge ? 0m : adjustment.Amount,
                    charge ? adjustment.Amount : 0m));
        }
        entries = entries.OrderBy(x => x.Date).ThenBy(x => x.Charge > 0m ? 0 : 1)
            .ThenBy(x => x.Type).ThenBy(x => x.DocumentId).ToList();

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

    private static string AdjustmentLabel(AdjustmentKind kind) => kind switch
    {
        AdjustmentKind.CreditNote => "Nota de crédito",
        AdjustmentKind.DebitNote => "Nota de débito",
        AdjustmentKind.OtherCredit => "Otro abono",
        AdjustmentKind.OtherCharge => "Otro cargo",
        _ => "Ajuste"
    };

    private sealed record Entry(DateOnly Date, string Type, int DocumentId, string Description,
        decimal Charge, decimal Credit);
}
