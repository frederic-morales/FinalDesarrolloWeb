namespace CuentasPorPagarAPI.Models;

public sealed class Payment
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int PaymentMethodId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = null!;
    public Guid RecordedByUserId { get; set; }
    public AppUser RecordedByUser { get; set; } = null!;
    public DateOnly Date { get; set; }
    public string? Reference { get; set; }
    public decimal Amount { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    public DateOnly? CancelledOn { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<PaymentAllocation> Allocations { get; set; } = [];
    public CheckDetail? CheckDetail { get; set; }
}
