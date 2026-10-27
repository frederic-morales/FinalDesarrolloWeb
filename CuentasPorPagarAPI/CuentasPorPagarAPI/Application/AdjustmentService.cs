using System.Data;
using CuentasPorPagarAPI.Contexts;
using CuentasPorPagarAPI.Contracts;
using CuentasPorPagarAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CuentasPorPagarAPI.Application;

public sealed class AdjustmentService(PayablesDbContext db)
{
    public async Task<AdjustmentResponse> CreateAsync(
        AdjustmentRequest request, Guid userId, CancellationToken cancellationToken)
    {
        var kind = Validate(request);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await db.Suppliers.AnyAsync(x => x.Id == request.SupplierId && x.IsActive, cancellationToken))
            throw new ApiException(400, "El proveedor no existe o está inactivo.");
        var series = request.Series.Trim();
        var number = request.Number.Trim();
        if (await db.SupplierAdjustments.AnyAsync(x => x.SupplierId == request.SupplierId &&
            x.Kind == kind && x.Series == series && x.Number == number, cancellationToken))
            throw new ApiException(409, "Ya existe este documento para el proveedor.");

        var ids = request.Allocations.Select(x => x.InvoiceId).ToArray();
        var invoices = await db.PurchaseInvoices.AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (invoices.Count != ids.Length || invoices.Any(x => x.SupplierId != request.SupplierId ||
            x.Status != DocumentStatus.Active || x.IssueDate > request.Date))
            throw new ApiException(400, "Las facturas deben estar activas, pertenecer al proveedor y existir en la fecha del ajuste.");

        if (IsCredit(kind))
        {
            var balances = await PayableQuery.At(db, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6)))
                .Where(x => ids.Contains(x.Invoice.Id))
                .Select(x => new { x.Invoice.Id, x.Invoice.Total, x.Paid, x.Debit, x.Credit })
                .ToDictionaryAsync(x => x.Id, cancellationToken);
            var balancesAtDate = await PayableQuery.At(db, request.Date)
                .Where(x => ids.Contains(x.Invoice.Id))
                .Select(x => new { x.Invoice.Id, x.Invoice.Total, x.Paid, x.Debit, x.Credit })
                .ToDictionaryAsync(x => x.Id, cancellationToken);
            foreach (var allocation in request.Allocations)
            {
                var row = balances[allocation.InvoiceId];
                var atDate = balancesAtDate[allocation.InvoiceId];
                if (allocation.Amount > row.Total + row.Debit - row.Credit - row.Paid ||
                    allocation.Amount > atDate.Total + atDate.Debit - atDate.Credit - atDate.Paid)
                    throw new ApiException(409, "La nota supera el saldo de una factura.");
            }
        }

        var adjustment = new SupplierAdjustment
        {
            SupplierId = request.SupplierId,
            RecordedByUserId = userId,
            Kind = kind,
            Series = series,
            Number = number,
            Date = request.Date,
            Description = request.Description.Trim(),
            Amount = request.Amount,
            Allocations = request.Allocations.Select(x => new AdjustmentAllocation
                { PurchaseInvoiceId = x.InvoiceId, Amount = x.Amount }).ToList()
        };
        db.SupplierAdjustments.Add(adjustment);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetAsync(adjustment.Id, cancellationToken))!;
    }

    public async Task CancelAsync(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var adjustment = await db.SupplierAdjustments.Include(x => x.Allocations)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiException(404, "Movimiento no encontrado.");
        if (adjustment.Status == DocumentStatus.Cancelled) return;
        if (!IsCredit(adjustment.Kind))
        {
            var ids = adjustment.Allocations.Select(x => x.PurchaseInvoiceId).ToArray();
            var balances = await PayableQuery.At(db, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6)))
                .Where(x => ids.Contains(x.Invoice.Id))
                .Select(x => new { x.Invoice.Id, x.Invoice.Total, x.Paid, x.Debit, x.Credit })
                .ToDictionaryAsync(x => x.Id, cancellationToken);
            foreach (var allocation in adjustment.Allocations)
            {
                var row = balances[allocation.PurchaseInvoiceId];
                if (row.Total + row.Debit - row.Credit - row.Paid < allocation.Amount)
                    throw new ApiException(409, "Anule primero los pagos o créditos que dependen de este cargo.");
            }
        }
        adjustment.Status = DocumentStatus.Cancelled;
        adjustment.CancelledOn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AdjustmentResponse?> GetAsync(int id, CancellationToken cancellationToken)
    {
        var adjustment = await db.SupplierAdjustments.AsNoTracking().Include(x => x.Supplier)
            .Include(x => x.Allocations).ThenInclude(x => x.PurchaseInvoice)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (adjustment is null) return null;
        return new AdjustmentResponse(adjustment.Id, adjustment.SupplierId, adjustment.Supplier.Name,
            adjustment.Kind.ToString(), adjustment.Series, adjustment.Number, adjustment.Date,
            adjustment.Description, adjustment.Amount, adjustment.Status.ToString(),
            adjustment.Allocations.OrderBy(x => x.Id)
                .Select(x => new AdjustmentAllocationResponse(x.PurchaseInvoiceId,
                    $"{x.PurchaseInvoice.Series}-{x.PurchaseInvoice.Number}", x.Amount)).ToList());
    }

    public async Task<PageResult<AdjustmentListResponse>> ListAsync(
        int? supplierId, AdjustmentKind? kind, DateOnly? from, DateOnly? to,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        if (from > to) throw new ApiException(400, "El rango de fechas es inválido.");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.SupplierAdjustments.AsNoTracking();
        if (supplierId is not null) query = query.Where(x => x.SupplierId == supplierId);
        if (kind is not null) query = query.Where(x => x.Kind == kind);
        if (from is not null) query = query.Where(x => x.Date >= from);
        if (to is not null) query = query.Where(x => x.Date <= to);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.SupplierId, SupplierName = x.Supplier.Name,
                x.Kind, x.Series, x.Number, x.Date, x.Amount, x.Status })
            .ToListAsync(cancellationToken);
        var items = rows.Select(x => new AdjustmentListResponse(x.Id, x.SupplierId, x.SupplierName,
            x.Kind.ToString(), x.Series, x.Number, x.Date, x.Amount, x.Status.ToString())).ToList();
        return new PageResult<AdjustmentListResponse>(items, total, page, pageSize);
    }

    private static bool IsCredit(AdjustmentKind kind) =>
        kind is AdjustmentKind.CreditNote or AdjustmentKind.OtherCredit;

    private static AdjustmentKind Validate(AdjustmentRequest request)
    {
        if (!Enum.GetNames<AdjustmentKind>().Contains(request.Kind, StringComparer.OrdinalIgnoreCase) ||
            !Enum.TryParse<AdjustmentKind>(request.Kind, true, out var kind))
            throw new ApiException(400, "Tipo de movimiento inválido.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));
        if (request.SupplierId <= 0 || request.Date == default || request.Date > today ||
            string.IsNullOrWhiteSpace(request.Series) || request.Series.Trim().Length > 30 ||
            string.IsNullOrWhiteSpace(request.Number) || request.Number.Trim().Length > 50 ||
            string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 300)
            throw new ApiException(400, "Proveedor, documento, descripción y fecha son obligatorios.");
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount ||
            request.Allocations is null || request.Allocations.Count == 0 ||
            request.Allocations.Any(x => x.InvoiceId <= 0 || x.Amount <= 0 ||
                decimal.Round(x.Amount, 2) != x.Amount) ||
            request.Allocations.Select(x => x.InvoiceId).Distinct().Count() != request.Allocations.Count ||
            request.Allocations.Sum(x => x.Amount) != request.Amount)
            throw new ApiException(400, "El importe debe ser positivo y estar aplicado por completo a facturas distintas.");
        return kind;
    }
}
