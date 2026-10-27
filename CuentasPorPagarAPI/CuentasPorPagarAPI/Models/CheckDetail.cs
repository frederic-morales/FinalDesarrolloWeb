namespace CuentasPorPagarAPI.Models;

public sealed class CheckDetail
{
    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public string BankName { get; set; } = string.Empty;
    public string CheckNumber { get; set; } = string.Empty;
    public DateOnly? ClearedOn { get; set; }
}
