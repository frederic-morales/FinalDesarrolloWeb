using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CuentasPorPagarAPI.Models;
using Microsoft.IdentityModel.Tokens;

namespace CuentasPorPagarAPI.Application;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "CuentasPorPagarAPI";
    public string Audience { get; set; } = "CuentasPorPagarFrontend";
    public string Key { get; set; } = string.Empty;
}

public sealed class JwtTokenService(JwtSettings settings)
{
    public (string Token, DateTime ExpiresAtUtc) Create(AppUser user)
    {
        var expires = DateTime.UtcNow.AddHours(8);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("security_stamp", user.SecurityStamp.ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
