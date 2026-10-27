namespace CuentasPorPagarAPI.Models;

public sealed class PaymentMethod
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool RequiresCheckDetails { get; set; }
}
