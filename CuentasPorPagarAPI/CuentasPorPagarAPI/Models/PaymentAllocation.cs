namespace CuentasPorPagarAPI.Models;

public sealed class PaymentAllocation
{
    public int Id { get; set; }
    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public int PurchaseInvoiceId { get; set; }
    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;
    public decimal Amount { get; set; }
}
