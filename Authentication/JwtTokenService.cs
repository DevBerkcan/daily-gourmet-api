using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DailyGourmet.Api.Authentication;

/// <summary>Claim keys shared between token generation and TenantContextMiddleware.</summary>
public static class DgClaimTypes
{
    public const string TenantId = "tenantId";
    public const string FacilityId = "facilityId";
    public const string IsImpersonation = "isImpersonation";
    public const string ImpersonatedBySuperAdminId = "impersonatedBy";
    public const string SupportSessionId = "supportSessionId";
}

public class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public string GenerateToken(User user)
    {
        if (string.IsNullOrWhiteSpace(_options.Secret))
            throw new InvalidOperationException("Jwt:Secret is not configured. Set it via environment variable or user-secrets, never commit it.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (user.TenantId is { } tenantId)
            claims.Add(new Claim(DgClaimTypes.TenantId, tenantId.ToString()));
        if (user.FacilityId is { } facilityId)
            claims.Add(new Claim(DgClaimTypes.FacilityId, facilityId.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateImpersonationToken(User superAdmin, Guid targetTenantId, Guid supportSessionId, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(_options.Secret))
            throw new InvalidOperationException("Jwt:Secret is not configured. Set it via environment variable or user-secrets, never commit it.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, superAdmin.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, superAdmin.Email),
            new(ClaimTypes.Name, superAdmin.Name),
            // Effective role while impersonating is TENANT_ADMIN — not SUPER_ADMIN — so this token
            // can't reach any SUPER_ADMIN-only endpoint, and every existing
            // [Authorize(Roles="TENANT_OWNER,TENANT_ADMIN")] check works without modification.
            new(ClaimTypes.Role, Role.TENANT_ADMIN.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(DgClaimTypes.TenantId, targetTenantId.ToString()),
            new(DgClaimTypes.IsImpersonation, "true"),
            new(DgClaimTypes.ImpersonatedBySuperAdminId, superAdmin.Id.ToString()),
            new(DgClaimTypes.SupportSessionId, supportSessionId.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
