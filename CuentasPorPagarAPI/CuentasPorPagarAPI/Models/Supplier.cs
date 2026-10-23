namespace CuentasPorPagarAPI.Models;

public sealed class Supplier
{
    public int Id { get; set; }
    public string? Nit { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<PurchaseInvoice> Invoices { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}
