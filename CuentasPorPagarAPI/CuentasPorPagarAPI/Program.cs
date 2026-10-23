using System.Security.Claims;
using System.Text;
using CuentasPorPagarAPI.Application;
using CuentasPorPagarAPI.Data;
using CuentasPorPagarAPI.Domain;
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

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var connection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection fuera del repositorio.");
builder.Services.AddDbContext<PayablesDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<PaymentService>();
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
            OnTokenValidated = async context =>
            {
                if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                {
                    context.Fail("Token inválido.");
                    return;
                }
                var db = context.HttpContext.RequestServices.GetRequiredService<PayablesDbContext>();
                var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
                if (user is null || !user.IsActive || user.Role != context.Principal?.FindFirstValue(ClaimTypes.Role) ||
                    user.SecurityStamp.ToString() != context.Principal?.FindFirstValue("security_stamp"))
                    context.Fail("La cuenta ya no está activa.");
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
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseHttpsRedirection();
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
