using System.Security.Claims;
using CuentasPorPagarAPI.Contracts;
using CuentasPorPagarAPI.Contexts;
using CuentasPorPagarAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CuentasPorPagarAPI.Controllers;

[ApiController]
[Authorize(Roles = Roles.Administrator)]
[Route("api/v1/users")]
public sealed class UsersController(PayablesDbContext db, IPasswordHasher<AppUser> passwordHasher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> List(CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking().OrderBy(x => x.UserName)
            .Select(x => new UserResponse(x.Id, x.UserName, x.Role, x.IsActive))
            .ToListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new UserResponse(x.Id, x.UserName, x.Role, x.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var name = request.UserName.Trim();
        if (name.Length == 0) return BadRequest("El nombre de usuario es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest("La contraseña es obligatoria.");
        if (request.Role is not (Roles.Administrator or Roles.Operator)) return BadRequest("Rol inválido.");
        if (await db.Users.AnyAsync(x => x.UserName == name, cancellationToken)) return Conflict("El usuario ya existe.");

        var user = new AppUser { UserName = name, Role = request.Role };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.SecurityStamp = Guid.NewGuid();
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, new UserResponse(user.Id, user.UserName, user.Role, user.IsActive));
    }

    [HttpPut("{id:guid}/password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([id], cancellationToken);
        if (user is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest("La contraseña es obligatoria.");
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.SecurityStamp = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, UserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([id], cancellationToken);
        if (user is null) return NotFound();
        if (!request.IsActive && User.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString())
            return Conflict("No puede desactivar su propia cuenta.");
        user.IsActive = request.IsActive;
        user.SecurityStamp = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
