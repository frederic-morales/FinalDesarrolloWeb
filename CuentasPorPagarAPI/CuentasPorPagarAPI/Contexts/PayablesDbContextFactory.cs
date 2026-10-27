using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CuentasPorPagarAPI.Contexts;

public sealed class PayablesDbContextFactory : IDesignTimeDbContextFactory<PayablesDbContext>
{
    public PayablesDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<PayablesDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection en User Secrets o variable de entorno antes de usar dotnet ef.");
        var options = new DbContextOptionsBuilder<PayablesDbContext>().UseNpgsql(connection).Options;
        return new PayablesDbContext(options);
    }
}
