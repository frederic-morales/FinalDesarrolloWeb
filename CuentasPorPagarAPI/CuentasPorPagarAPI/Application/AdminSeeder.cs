using CuentasPorPagarAPI.Data;
using CuentasPorPagarAPI.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CuentasPorPagarAPI.Application;

public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayablesDbContext>();
        if (await db.Users.AnyAsync())
        {
            throw new InvalidOperationException("Ya existen usuarios. AdminSeeder solo crea el primer administrador.");
        }

        var userName = configuration["SeedAdmin:UserName"]?.Trim();
        var password = configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new InvalidOperationException("Configure SeedAdmin:UserName y SeedAdmin:Password (mínimo 12 caracteres) fuera del repositorio.");
        }

        var user = new AppUser { UserName = userName, Role = Roles.Administrator };
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}
