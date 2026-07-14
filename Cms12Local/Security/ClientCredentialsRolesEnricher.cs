using System.Security.Claims;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Cms12Local.Security;

// Embeds editor role claims directly into access tokens issued via the client_credentials grant.
// EPiServer's content access evaluator reads roles from the validated principal via
// IPrincipal.IsInRole; without them, the service-account token authenticates but the
// Content Management API rejects it with 403 Forbidden on the ACL check for the content item.
//
// This handler runs between OpenIddict's PrepareAccessTokenPrincipal (which builds
// AccessTokenPrincipal from context.Principal) and GenerateIdentityModelAccessToken (which
// serializes AccessTokenPrincipal into the JWT). Claims added here therefore end up inside
// the issued access token itself.
public sealed class ClientCredentialsRolesEnricher : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    // ClaimTypes.Role is used by ASP.NET Identity; "role" is the OpenID Connect standard claim
    // (and the RoleClaimType configured by EPiServer.OpenIDConnect); "roles" is used by some
    // OpenIddict/Azure AD conventions. Emitting all three keeps role checks working regardless
    // of which claim type a consumer inspects.
    private static readonly string[] RoleClaimTypes =
    [
        ClaimTypes.Role,
        "role",
        "roles",
    ];

    private static readonly string[] EditorRoles =
    [
        "Administrators",
        "WebEditors",
        "WebAdmins",
        "CmsEditors",
        "CmsAdmins",
    ];

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ProcessSignInContext>()
            .UseSingletonHandler<ClientCredentialsRolesEnricher>()
            .SetOrder(OpenIddictServerHandlers.PrepareAccessTokenPrincipal.Descriptor.Order + 500)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    private readonly ILogger<ClientCredentialsRolesEnricher> _logger;

    public ClientCredentialsRolesEnricher(ILogger<ClientCredentialsRolesEnricher> logger)
    {
        _logger = logger;
    }

    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (context.Request?.GrantType != OpenIddictConstants.GrantTypes.ClientCredentials)
            return default;

        if (context.AccessTokenPrincipal?.Identity is not ClaimsIdentity identity)
            return default;

        foreach (var role in EditorRoles)
        {
            foreach (var claimType in RoleClaimTypes)
            {
                if (context.AccessTokenPrincipal.HasClaim(claimType, role))
                    continue;

                identity.AddClaim(new Claim(claimType, role));
            }
        }

        _logger.LogInformation(
            "ClientCredentialsRolesEnricher: injected editor roles into CC token for client={ClientId}",
            context.Request.ClientId);

        return default;
    }
}
