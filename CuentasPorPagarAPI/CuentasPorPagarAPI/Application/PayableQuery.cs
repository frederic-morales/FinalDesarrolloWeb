using CuentasPorPagarAPI.Contexts;
using CuentasPorPagarAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CuentasPorPagarAPI.Application;

public static class PayableQuery
{
    public static IQueryable<PayableRow> At(PayablesDbContext db, DateOnly asOf) =>
        db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.IssueDate <= asOf &&
                (x.Status == DocumentStatus.Active || x.CancelledOn > asOf))
            .Select(x => new PayableRow
            {
                Invoice = x,
                Paid = x.Allocations
                    .Where(a => a.Payment.Date <= asOf &&
                        (a.Payment.Status == DocumentStatus.Active || a.Payment.CancelledOn > asOf))
                    .Sum(a => (decimal?)a.Amount) ?? 0m,
                Debit = x.AdjustmentAllocations
                    .Where(a => a.SupplierAdjustment.Date <= asOf &&
                        (a.SupplierAdjustment.Status == DocumentStatus.Active ||
                         a.SupplierAdjustment.CancelledOn > asOf) &&
                        (a.SupplierAdjustment.Kind == AdjustmentKind.DebitNote ||
                         a.SupplierAdjustment.Kind == AdjustmentKind.OtherCharge))
                    .Sum(a => (decimal?)a.Amount) ?? 0m,
                Credit = x.AdjustmentAllocations
                    .Where(a => a.SupplierAdjustment.Date <= asOf &&
                        (a.SupplierAdjustment.Status == DocumentStatus.Active ||
                         a.SupplierAdjustment.CancelledOn > asOf) &&
                        (a.SupplierAdjustment.Kind == AdjustmentKind.CreditNote ||
                         a.SupplierAdjustment.Kind == AdjustmentKind.OtherCredit))
                    .Sum(a => (decimal?)a.Amount) ?? 0m
            });
}

public sealed class PayableRow
{
    public PurchaseInvoice Invoice { get; init; } = null!;
    public decimal Paid { get; init; }
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public decimal Balance => Invoice.Total + Debit - Credit - Paid;
}
