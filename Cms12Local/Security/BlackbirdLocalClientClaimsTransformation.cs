using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;

namespace Cms12Local.Security;

// Safety net for the local Blackbird OpenID Connect clients: ensures the validated principal
// carries editor role claims and a stable name/nameidentifier so EPiServer's ACL evaluator
// grants access to Content Management API endpoints.
//
// For the client_credentials flow, ClientCredentialsRolesEnricher already bakes these role
// claims into the issued JWT, so this transformation is mostly a no-op there. For the
// resource-owner-password flow with an OpenID Connect user that lacks the expected role
// mapping, this transformation still fills in the gap.
public class BlackbirdLocalClientClaimsTransformation : IClaimsTransformation
{
    private static readonly HashSet<string> LocalClientIds = new(StringComparer.Ordinal)
    {
        "blackbird-local",
        "blackbird-cc",
    };

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
        "Administrators",
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
            .FirstOrDefault(value => value is not null && LocalClientIds.Contains(value));

        if (clientId is null)
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

        foreach (var claim in claims)
        {
            identity.AddClaim(claim);
        }

        return Task.FromResult(principal);
    }
}
