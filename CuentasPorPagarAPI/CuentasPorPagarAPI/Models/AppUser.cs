namespace CuentasPorPagarAPI.Models;

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
