namespace CuentasPorPagarAPI.Domain;

public enum DocumentStatus { Active = 1, Cancelled = 2 }

public static class Roles
{
    public const string Administrator = "Administrador";
    public const string Operator = "Operador";
}

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();
    public string Role { get; set; } = Roles.Operator;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

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

public sealed class PaymentMethod
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

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
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<InvoiceLine> Lines { get; set; } = [];
    public List<PaymentAllocation> Allocations { get; set; } = [];
}

public sealed class InvoiceLine
{
    public int Id { get; set; }
    public int PurchaseInvoiceId { get; set; }
    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

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
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<PaymentAllocation> Allocations { get; set; } = [];
}

public sealed class PaymentAllocation
{
    public int Id { get; set; }
    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public int PurchaseInvoiceId { get; set; }
    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;
    public decimal Amount { get; set; }
}
