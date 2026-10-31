using System.Security.Claims;
using System.Text;
using CuentasPorPagarAPI.Application;
using CuentasPorPagarAPI.Contexts;
using CuentasPorPagarAPI.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var seedAdmin = args.Contains("--seed-admin", StringComparer.OrdinalIgnoreCase);
var builder = WebApplication.CreateBuilder(args.Where(x => !x.Equals("--seed-admin", StringComparison.OrdinalIgnoreCase)).ToArray());
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddResponseCompression(o => o.EnableForHttps = true);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var connection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection fuera del repositorio.");
builder.Services.AddDbContext<PayablesDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<AdjustmentService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

var jwt = new JwtSettings
{
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "CuentasPorPagarAPI",
    Audience = builder.Configuration["Jwt:Audience"] ?? "CuentasPorPagarFrontend",
    Key = builder.Configuration["Jwt:Key"] ?? string.Empty
};
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Configure Jwt:Key con al menos 32 bytes fuera del repositorio.");
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtValidation").LogWarning(context.Exception,
                        "No se pudo validar la firma o vigencia del token.");
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtValidation");
                if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                {
                    logger.LogWarning("Token sin identificador de usuario válido.");
                    context.Fail("Token inválido.");
                    return;
                }
                var db = context.HttpContext.RequestServices.GetRequiredService<PayablesDbContext>();
                var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
                if (user is null || !user.IsActive)
                {
                    logger.LogWarning("Token de usuario inexistente o inactivo.");
                    context.Fail("La cuenta ya no está activa.");
                    return;
                }
                if (user.Role != context.Principal?.FindFirstValue(ClaimTypes.Role))
                {
                    logger.LogWarning("Token con rol distinto al usuario actual.");
                    context.Fail("El rol del usuario cambió.");
                    return;
                }
                if (user.SecurityStamp.ToString() != context.Principal?.FindFirstValue("security_stamp"))
                {
                    logger.LogWarning("Token con sello de seguridad distinto al usuario actual.");
                    context.Fail("Las credenciales del usuario cambiaron.");
                }
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").GetChildren()
    .Select(x => x.Value).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray();
builder.Services.AddCors(options => options.AddPolicy("Angular", policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseResponseCompression();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
// Render termina TLS antes de enviar la solicitud HTTP al contenedor.
if (app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseCors("Angular");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/health/ready", async (PayablesDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));

if (seedAdmin)
{
    await AdminSeeder.SeedAsync(app.Services, app.Configuration);
    Console.WriteLine("Administrador inicial creado.");
    return;
}

await app.RunAsync();
