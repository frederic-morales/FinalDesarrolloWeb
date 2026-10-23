using System.Security.Claims;
using CuentasPorPagarAPI.Application;
using CuentasPorPagarAPI.Contracts;
using CuentasPorPagarAPI.Data;
using CuentasPorPagarAPI.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace CuentasPorPagarAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(PayablesDbContext db, IPasswordHasher<AppUser> passwordHasher, JwtTokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var userName = request.UserName?.Trim();
        var user = await db.Users.SingleOrDefaultAsync(x => x.UserName == userName && x.IsActive, cancellationToken);
        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Credenciales inválidas." });
        }

        var (token, expires) = tokens.Create(user);
        return Ok(new LoginResponse(token, expires, new UserResponse(user.Id, user.UserName, user.Role, user.IsActive)));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
        return user is null ? Unauthorized() : Ok(new UserResponse(user.Id, user.UserName, user.Role, user.IsActive));
    }
}
