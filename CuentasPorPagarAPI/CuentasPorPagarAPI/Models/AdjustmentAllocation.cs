namespace CuentasPorPagarAPI.Models;

public sealed class AdjustmentAllocation
{
    public int Id { get; set; }
    public int SupplierAdjustmentId { get; set; }
    public SupplierAdjustment SupplierAdjustment { get; set; } = null!;
    public int PurchaseInvoiceId { get; set; }
    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;
    public decimal Amount { get; set; }
}
