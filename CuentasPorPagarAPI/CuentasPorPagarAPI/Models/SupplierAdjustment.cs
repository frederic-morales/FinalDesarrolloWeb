namespace CuentasPorPagarAPI.Models;

public sealed class SupplierAdjustment
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public Guid RecordedByUserId { get; set; }
    public AppUser RecordedByUser { get; set; } = null!;
    public AdjustmentKind Kind { get; set; }
    public string Series { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    public DateOnly? CancelledOn { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<AdjustmentAllocation> Allocations { get; set; } = [];
}
