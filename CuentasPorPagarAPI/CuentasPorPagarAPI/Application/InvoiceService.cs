using System.Data;
using CuentasPorPagarAPI.Contracts;
using CuentasPorPagarAPI.Contexts;
using CuentasPorPagarAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CuentasPorPagarAPI.Application;

public sealed class InvoiceService(PayablesDbContext db)
{
    public async Task<InvoiceResponse> CreateAsync(InvoiceRequest request, CancellationToken cancellationToken)
    {
        var (series, number, lines, total) = Prepare(request);
        if (!await db.Suppliers.AnyAsync(x => x.Id == request.SupplierId && x.IsActive, cancellationToken))
            throw new ApiException(400, "El proveedor no existe o está inactivo.");
        if (await IsDuplicateAsync(request.SupplierId, series, number, null, cancellationToken))
            throw new ApiException(409, "Ya existe esta factura para el proveedor.");

        var invoice = new PurchaseInvoice
        {
            SupplierId = request.SupplierId, Series = series, Number = number,
            IssueDate = request.IssueDate, DueDate = request.DueDate, Total = total, Lines = lines
        };
        db.PurchaseInvoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(invoice.Id, cancellationToken))!;
    }

    public async Task<InvoiceResponse> UpdateAsync(int id, InvoiceRequest request, CancellationToken cancellationToken)
    {
        var (series, number, lines, total) = Prepare(request);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var invoice = await db.PurchaseInvoices.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiException(404, "Factura no encontrada.");
        if (invoice.Status != DocumentStatus.Active) throw new ApiException(409, "La factura está anulada.");
        if (await db.PaymentAllocations.AnyAsync(x => x.PurchaseInvoiceId == id, cancellationToken) ||
            await db.AdjustmentAllocations.AnyAsync(x => x.PurchaseInvoiceId == id, cancellationToken))
            throw new ApiException(409, "Una factura con movimientos registrados no puede modificarse. Anúlela si corresponde.");
        if (!await db.Suppliers.AnyAsync(x => x.Id == request.SupplierId && x.IsActive, cancellationToken))
            throw new ApiException(400, "El proveedor no existe o está inactivo.");
        if (await IsDuplicateAsync(request.SupplierId, series, number, id, cancellationToken))
            throw new ApiException(409, "Ya existe esta factura para el proveedor.");

        db.InvoiceLines.RemoveRange(invoice.Lines);
        invoice.SupplierId = request.SupplierId;
        invoice.Series = series;
        invoice.Number = number;
        invoice.IssueDate = request.IssueDate;
        invoice.DueDate = request.DueDate;
        invoice.Total = total;
        invoice.Lines = lines;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetAsync(id, cancellationToken))!;
    }

    public async Task CancelAsync(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var invoice = await db.PurchaseInvoices.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiException(404, "Factura no encontrada.");
        if (invoice.Status == DocumentStatus.Cancelled) return;
        if (await db.PaymentAllocations.AnyAsync(x => x.PurchaseInvoiceId == id && x.Payment.Status == DocumentStatus.Active, cancellationToken))
            throw new ApiException(409, "Anule primero los pagos aplicados a esta factura.");
        if (await db.AdjustmentAllocations.AnyAsync(x => x.PurchaseInvoiceId == id &&
            x.SupplierAdjustment.Status == DocumentStatus.Active, cancellationToken))
            throw new ApiException(409, "Anule primero las notas y ajustes aplicados a esta factura.");
        invoice.Status = DocumentStatus.Cancelled;
        invoice.CancelledOn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<InvoiceResponse?> GetAsync(int id, CancellationToken cancellationToken)
    {
        var invoice = await db.PurchaseInvoices.AsNoTracking()
            .Include(x => x.Supplier).Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));
        var amounts = await PayableQuery.At(db, today).Where(x => x.Invoice.Id == id)
            .Select(x => new { x.Paid, x.Debit, x.Credit }).SingleOrDefaultAsync(cancellationToken);
        var paid = amounts?.Paid ?? 0m;
        var debit = amounts?.Debit ?? 0m;
        var credit = amounts?.Credit ?? 0m;
        return new InvoiceResponse(invoice.Id, invoice.SupplierId, invoice.Supplier.Name, invoice.Series,
            invoice.Number, invoice.IssueDate, invoice.DueDate, invoice.Total, paid,
            invoice.Status == DocumentStatus.Active ? invoice.Total + debit - credit - paid : 0m, invoice.Status.ToString(),
            invoice.Lines.OrderBy(x => x.Id).Select(x => new InvoiceLineResponse(x.Id, x.Description,
                x.Quantity, x.UnitPrice, x.Subtotal)).ToList(), debit, credit);
    }

    public async Task<PageResult<InvoiceListResponse>> ListAsync(
        int? supplierId, bool? pending, bool? overdue, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));
        var query = db.PurchaseInvoices.AsNoTracking().Include(x => x.Supplier).AsQueryable();
        if (supplierId is not null) query = query.Where(x => x.SupplierId == supplierId);
        if (overdue == true) query = query.Where(x => x.Status == DocumentStatus.Active && x.DueDate < today);

        var projected = query.Select(x => new
        {
            Invoice = x,
            Paid = x.Allocations.Where(a => a.Payment.Status == DocumentStatus.Active)
                .Sum(a => (decimal?)a.Amount) ?? 0m,
            Debit = x.AdjustmentAllocations.Where(a =>
                a.SupplierAdjustment.Status == DocumentStatus.Active &&
                (a.SupplierAdjustment.Kind == AdjustmentKind.DebitNote ||
                 a.SupplierAdjustment.Kind == AdjustmentKind.OtherCharge))
                .Sum(a => (decimal?)a.Amount) ?? 0m,
            Credit = x.AdjustmentAllocations.Where(a =>
                a.SupplierAdjustment.Status == DocumentStatus.Active &&
                (a.SupplierAdjustment.Kind == AdjustmentKind.CreditNote ||
                 a.SupplierAdjustment.Kind == AdjustmentKind.OtherCredit))
                .Sum(a => (decimal?)a.Amount) ?? 0m
        });
        if (pending == true || overdue == true)
            projected = projected.Where(x => x.Invoice.Status == DocumentStatus.Active &&
                x.Invoice.Total + x.Debit > x.Paid + x.Credit);
        if (pending == false)
            projected = projected.Where(x => x.Invoice.Status == DocumentStatus.Cancelled ||
                x.Invoice.Total + x.Debit <= x.Paid + x.Credit);

        var total = await projected.CountAsync(cancellationToken);
        var rows = await projected.OrderByDescending(x => x.Invoice.IssueDate).ThenByDescending(x => x.Invoice.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = rows.Select(x => new InvoiceListResponse(
            x.Invoice.Id, x.Invoice.SupplierId, x.Invoice.Supplier.Name, x.Invoice.Series, x.Invoice.Number,
            x.Invoice.IssueDate, x.Invoice.DueDate, x.Invoice.Total, x.Paid,
            x.Invoice.Status == DocumentStatus.Active ? x.Invoice.Total + x.Debit - x.Credit - x.Paid : 0m,
            x.Invoice.Status.ToString(), x.Invoice.Status == DocumentStatus.Active &&
            x.Invoice.DueDate < today && x.Invoice.Total + x.Debit > x.Paid + x.Credit,
            x.Debit, x.Credit)).ToList();
        return new PageResult<InvoiceListResponse>(items, total, page, pageSize);
    }

    private async Task<bool> IsDuplicateAsync(int supplierId, string series, string number, int? excludeId, CancellationToken cancellationToken) =>
        await db.PurchaseInvoices.AnyAsync(x => x.SupplierId == supplierId && x.Series == series &&
            x.Number == number && (excludeId == null || x.Id != excludeId), cancellationToken);

    private static (string Series, string Number, List<InvoiceLine> Lines, decimal Total) Prepare(InvoiceRequest request)
    {
        var series = request.Series?.Trim() ?? string.Empty;
        var number = request.Number?.Trim() ?? string.Empty;
        if (request.SupplierId <= 0 || series.Length == 0 || number.Length == 0)
            throw new ApiException(400, "Proveedor, serie y número son obligatorios.");
        if (request.IssueDate == default || request.DueDate < request.IssueDate)
            throw new ApiException(400, "Las fechas de emisión y vencimiento son inválidas.");
        if (request.Lines is null || request.Lines.Count == 0)
            throw new ApiException(400, "La factura debe tener al menos un concepto.");

        var lines = new List<InvoiceLine>();
        foreach (var item in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(item.Description) || item.Quantity <= 0 || item.UnitPrice <= 0 ||
                decimal.Round(item.Quantity, 3) != item.Quantity || decimal.Round(item.UnitPrice, 2) != item.UnitPrice)
                throw new ApiException(400, "Cada concepto requiere descripción, cantidad positiva (3 decimales) y precio positivo (2 decimales).");
            var subtotal = decimal.Round(item.Quantity * item.UnitPrice, 2, MidpointRounding.AwayFromZero);
            if (subtotal <= 0) throw new ApiException(400, "El subtotal de un concepto debe ser mayor que cero.");
            lines.Add(new InvoiceLine { Description = item.Description.Trim(), Quantity = item.Quantity,
                UnitPrice = item.UnitPrice, Subtotal = subtotal });
        }
        var total = lines.Sum(x => x.Subtotal);
        return (series, number, lines, total);
    }
}
