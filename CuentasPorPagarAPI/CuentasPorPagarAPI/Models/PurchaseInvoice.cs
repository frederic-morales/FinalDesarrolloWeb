namespace CuentasPorPagarAPI.Models;

public sealed class PurchaseInvoice
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public string Series { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Total { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    public DateOnly? CancelledOn { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<InvoiceLine> Lines { get; set; } = [];
    public List<PaymentAllocation> Allocations { get; set; } = [];
    public List<AdjustmentAllocation> AdjustmentAllocations { get; set; } = [];
}
