using BookStore.Api.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BookStore.Api.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(Account account, IEnumerable<string>? permissions = null)
    {
        return GenerateToken(account.Email, account.Role.Name, account.AccountId, permissions);
    }

    public string GenerateToken(string email, string role, long accountId = 0, IEnumerable<string>? permissions = null)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, email),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        if (accountId > 0)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, accountId.ToString()));
        }

        // permissions claim - space separated for compactness
        if (permissions != null)
        {
            var scope = string.Join(' ', permissions.Distinct().OrderBy(x => x));
            if (!string.IsNullOrWhiteSpace(scope))
            {
                claims.Add(new Claim("permissions", scope));
            }
        }

        var expireDays = int.Parse(jwtSettings["ExpireDays"]!);
        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(expireDays),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
