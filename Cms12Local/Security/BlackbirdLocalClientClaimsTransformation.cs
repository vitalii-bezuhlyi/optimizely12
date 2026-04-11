using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;

namespace Cms12Local.Security;

public class BlackbirdLocalClientClaimsTransformation : IClaimsTransformation
{
    private const string LocalClientId = "blackbird-local";
    private static readonly string[] ClientIdentifierClaimTypes =
    [
        "client_id",
        "sub",
        "azp",
        ClaimTypes.NameIdentifier
    ];

    private static readonly string[] RoleClaimTypes =
    [
        ClaimTypes.Role,
        "role",
        "roles"
    ];

    private static readonly string[] RoleNames =
    [
        "WebEditors",
        "WebAdmins",
        "CmsEditors",
        "CmsAdmins"
    ];

    private readonly IWebHostEnvironment _environment;

    public BlackbirdLocalClientClaimsTransformation(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (!_environment.IsDevelopment())
        {
            return Task.FromResult(principal);
        }

        var identity = principal.Identities.FirstOrDefault(x => x.IsAuthenticated);
        if (identity is null)
        {
            return Task.FromResult(principal);
        }

        var clientId = ClientIdentifierClaimTypes
            .Select(claimType => principal.FindFirst(claimType)?.Value)
            .FirstOrDefault(value => string.Equals(value, LocalClientId, StringComparison.Ordinal));

        if (!string.Equals(clientId, LocalClientId, StringComparison.Ordinal))
        {
            return Task.FromResult(principal);
        }

        var claims = new List<Claim>();

        if (!principal.HasClaim(identity.NameClaimType, clientId))
        {
            claims.Add(new Claim(identity.NameClaimType, clientId));
        }

        if (!principal.HasClaim(ClaimTypes.NameIdentifier, clientId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, clientId));
        }

        foreach (var roleName in RoleNames)
        {
            if (!principal.HasClaim(identity.RoleClaimType, roleName))
            {
                claims.Add(new Claim(identity.RoleClaimType, roleName));
            }

            foreach (var roleClaimType in RoleClaimTypes.Where(roleClaimType => roleClaimType != identity.RoleClaimType))
            {
                if (!principal.HasClaim(roleClaimType, roleName))
                {
                    claims.Add(new Claim(roleClaimType, roleName));
                }
            }
        }

        if (claims.Count == 0)
        {
            return Task.FromResult(principal);
        }

        principal.AddIdentity(new ClaimsIdentity(claims, identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType));
        return Task.FromResult(principal);
    }
}
