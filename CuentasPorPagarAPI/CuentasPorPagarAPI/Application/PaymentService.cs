using System.Data;
using CuentasPorPagarAPI.Contracts;
using CuentasPorPagarAPI.Contexts;
using CuentasPorPagarAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CuentasPorPagarAPI.Application;

public sealed class PaymentService(PayablesDbContext db)
{
    public async Task<PaymentResponse> CreateAsync(PaymentRequest request, Guid userId, CancellationToken cancellationToken)
    {
        Validate(request);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (!await db.Suppliers.AnyAsync(x => x.Id == request.SupplierId && x.IsActive, cancellationToken))
            throw new ApiException(400, "El proveedor no existe o está inactivo.");
        var method = await db.PaymentMethods.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.PaymentMethodId && x.IsActive, cancellationToken)
            ?? throw new ApiException(400, "El método de pago no existe o está inactivo.");
        if (method.RequiresCheckDetails)
        {
            if (request.Check is null || string.IsNullOrWhiteSpace(request.Check.BankName) ||
                string.IsNullOrWhiteSpace(request.Check.CheckNumber) ||
                request.Check.BankName.Trim().Length > 120 || request.Check.CheckNumber.Trim().Length > 60 ||
                request.Check.ClearedOn < request.Date ||
                request.Check.ClearedOn > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6)))
                throw new ApiException(400, "El cheque requiere banco, número y fecha de cobro válida si se indica.");
        }
        else if (request.Check is not null)
            throw new ApiException(400, "Solo un método de cheque puede incluir datos de cheque.");

        var ids = request.Allocations.Select(x => x.InvoiceId).ToArray();
        var invoices = await db.PurchaseInvoices.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        if (invoices.Count != ids.Length || invoices.Any(x => x.SupplierId != request.SupplierId ||
            x.Status != DocumentStatus.Active || x.IssueDate > request.Date))
            throw new ApiException(400, "Todas las facturas deben estar activas, pertenecer al proveedor y existir en la fecha del pago.");

        var balances = await PayableQuery.At(db, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6)))
            .Where(x => ids.Contains(x.Invoice.Id))
            .Select(x => new { x.Invoice.Id, x.Invoice.Total, x.Paid, x.Debit, x.Credit })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var balancesAtPaymentDate = await PayableQuery.At(db, request.Date)
            .Where(x => ids.Contains(x.Invoice.Id))
            .Select(x => new { x.Invoice.Id, x.Invoice.Total, x.Paid, x.Debit, x.Credit })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var allocation in request.Allocations)
        {
            var invoice = invoices.Single(x => x.Id == allocation.InvoiceId);
            var amounts = balances[invoice.Id];
            var atDate = balancesAtPaymentDate[invoice.Id];
            if (allocation.Amount > amounts.Total + amounts.Debit - amounts.Credit - amounts.Paid ||
                allocation.Amount > atDate.Total + atDate.Debit - atDate.Credit - atDate.Paid)
                throw new ApiException(409, $"El abono supera el saldo de la factura {invoice.Series}-{invoice.Number}.");
        }

        var payment = new Payment
        {
            SupplierId = request.SupplierId,
            PaymentMethodId = request.PaymentMethodId,
            RecordedByUserId = userId,
            Date = request.Date,
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
            Amount = request.Amount,
            CheckDetail = request.Check is null ? null : new CheckDetail
            {
                BankName = request.Check.BankName.Trim(),
                CheckNumber = request.Check.CheckNumber.Trim(),
                ClearedOn = request.Check.ClearedOn
            },
            Allocations = request.Allocations.Select(x => new PaymentAllocation
            {
                PurchaseInvoiceId = x.InvoiceId, Amount = x.Amount
            }).ToList()
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetAsync(payment.Id, cancellationToken))!;
    }

    public async Task CancelAsync(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var payment = await db.Payments.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiException(404, "Pago no encontrado.");
        if (payment.Status == DocumentStatus.Cancelled) return;
        payment.Status = DocumentStatus.Cancelled;
        payment.CancelledOn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PaymentResponse?> GetAsync(int id, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.AsNoTracking().Include(x => x.Supplier)
            .Include(x => x.PaymentMethod).Include(x => x.CheckDetail)
            .Include(x => x.Allocations).ThenInclude(x => x.PurchaseInvoice)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (payment is null) return null;
        return new PaymentResponse(payment.Id, payment.SupplierId, payment.Supplier.Name,
            payment.PaymentMethodId, payment.PaymentMethod.Name, payment.Date, payment.Reference,
            payment.Amount, payment.Status.ToString(), payment.Allocations.OrderBy(x => x.Id)
                .Select(x => new PaymentAllocationResponse(x.PurchaseInvoiceId,
                    $"{x.PurchaseInvoice.Series}-{x.PurchaseInvoice.Number}", x.Amount)).ToList(),
            payment.CheckDetail is null ? null : new CheckDetailResponse(
                payment.CheckDetail.BankName, payment.CheckDetail.CheckNumber, payment.CheckDetail.ClearedOn));
    }

    public async Task<PageResult<PaymentListResponse>> ListAsync(
        int? supplierId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (from > to) throw new ApiException(400, "El rango de fechas es inválido.");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Payments.AsNoTracking();
        if (supplierId is not null) query = query.Where(x => x.SupplierId == supplierId);
        if (from is not null) query = query.Where(x => x.Date >= from);
        if (to is not null) query = query.Where(x => x.Date <= to);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new PaymentListResponse(x.Id, x.SupplierId, x.Supplier.Name,
                x.PaymentMethod.Name, x.Date, x.Reference, x.Amount, x.Status.ToString()))
            .ToListAsync(cancellationToken);
        return new PageResult<PaymentListResponse>(items, total, page, pageSize);
    }

    private static void Validate(PaymentRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));
        if (request.SupplierId <= 0 || request.PaymentMethodId <= 0 || request.Date == default || request.Date > today)
            throw new ApiException(400, "Proveedor, método y fecha del pago son inválidos.");
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount)
            throw new ApiException(400, "El importe del pago debe ser positivo y tener máximo dos decimales.");
        if (request.Allocations is null || request.Allocations.Count == 0)
            throw new ApiException(400, "El pago debe aplicarse a una o más facturas.");
        if (request.Allocations.Any(x => x.InvoiceId <= 0 || x.Amount <= 0 || decimal.Round(x.Amount, 2) != x.Amount) ||
            request.Allocations.Select(x => x.InvoiceId).Distinct().Count() != request.Allocations.Count)
            throw new ApiException(400, "Las aplicaciones deben tener facturas únicas e importes positivos de dos decimales.");
        if (request.Allocations.Sum(x => x.Amount) != request.Amount)
            throw new ApiException(400, "La suma de las aplicaciones debe coincidir con el importe del pago.");
    }
}
