# Blackbird.io Optimizely

Blackbird is the new automation backbone for the language technology industry. Blackbird provides enterprise-scale automation and orchestration with a simple no-code/low-code platform. Blackbird enables ambitious organizations to identify, vet and automate as many processes as possible. Not just localization workflows, but any business and IT process. This repository represents an application that is deployable on Blackbird and usable inside the workflow editor.

## Introduction

<!-- begin docs -->

Optimizely Content Management System (CMS) is a digital experience platform for managing multilingual website content. This app connects Blackbird to the Optimizely CMS Management API so you can search content, export localizable fields into a Blackbird-compatible HTML file, translate that file in a TMS, and upload the translated result back to Optimizely.

## Before setting up

Before you connect the app, make sure that:

- Your Optimizely instance is reachable from Blackbird.
- You have a username/password that can access the CMS management API.
- You have a **Client ID** and **Client secret** registered in Optimizely (see below).
- The languages you want to update already exist in the Optimizely site configuration.

### Where do the Client ID and Client secret come from?

Unlike many services, Optimizely does **not** give you a Client ID and Client secret from a settings page. Instead, a developer with access to your Optimizely solution **defines these values themselves** when registering an API client in the application's startup code. Think of it as creating a dedicated "login" that Blackbird (and only Blackbird) will use to talk to your CMS.

So the Client ID and Client secret are simply two values your team chooses and registers in Optimizely. The Client ID is a readable name (for example `blackbird-integration`), and the Client secret is a long, random, secret string — treat it like a password.

To register them, your developer adds an OpenID Connect application during service configuration (usually in `Startup.cs`). The Content Management API must also be enabled and pointed at the OpenID Connect authentication scheme. A minimal example:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // ... existing CMS / MVC setup ...

    // 0. Local CMS accounts (ASP.NET Identity) must stay registered — the
    //    token endpoint used by this app depends on it. Keep this line even
    //    if your editors sign in through Opti ID.
    services.AddCmsAspNetIdentity<ApplicationUser>();

    // 1. Enable the APIs this app uses and tell them to authenticate
    //    using OpenID Connect bearer tokens.
    services.AddContentManagementApi(
        OpenIDConnectOptionsDefaults.AuthenticationScheme);

    services.AddContentDeliveryApi(
        OpenIDConnectOptionsDefaults.AuthenticationScheme,
        options => options.SiteDefinitionApiEnabled = true); // required by "Search content" and "Search languages"

    // 2. Configure OpenID Connect and register the Blackbird client.
    services.AddOpenIDConnect<ApplicationUser>(
        useDevelopmentCertificate: true,   // dev only; use real certificates in production
        signingCertificate: null,
        encryptionCertificate: null,
        createSchema: true,
        options =>
        {
            // Local/dev only — keep RequireHttps = true in production.
            options.RequireHttps = false;

            // These two values are your Client ID and Client secret.
            options.Applications.Add(new OpenIDConnectApplication
            {
                ClientId = "blackbird-integration", // `blackbird-integration` -> this is just an example; you can choose any name
                ClientSecret = "replace-with-a-long-random-secret",
                Scopes =
                {
                    "openid",
                    "offline_access",
                    "profile",
                    "email",
                    "roles",
                    ContentManagementApiOptionsDefaults.Scope, // allows content management
                }
            });

            // Only needed for the "Username & password" connection type.
            // If you use the "Client credentials" connection type, remove this line.
            options.AllowResourceOwnerPasswordFlow = true;
        });
}
```

#### Extra step for the "Client credentials" connection type

With client credentials the app authenticates as an *application*, not as a user, so the token Optimizely issues carries no editor roles. Any Content Management API call the app makes will then fail with **`403 Forbidden`** (`detail: "Forbidden"`) — the token authenticates fine, but the ACL check on the target content item rejects the principal because it has no editor role.

Fixing this reliably requires the editor roles to be **inside the issued JWT itself**. Adding a runtime `IClaimsTransformation` on its own is **not enough**: OpenIddict re-decodes the bearer token into a fresh `ClaimsIdentity` on every `AuthenticateAsync` call in the pipeline, so mutations from one auth pass do not survive to the ACL check performed inside the Content Management API controller.

The fix is a small OpenIddict server-event handler that runs during token issuance, between `PrepareAccessTokenPrincipal` (where OpenIddict clones the sign-in principal into `AccessTokenPrincipal`) and `GenerateIdentityModelAccessToken` (where that principal is serialized into the JWT). Claims added there end up embedded in the token itself, so every subsequent request already carries them and the Content Management API's ACL evaluator grants access.

##### 1. Add the OpenIddict handler

Create a small handler class that injects the editor role claims for the client-credentials grant:

```csharp
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;

public sealed class BlackbirdClientCredentialsRolesEnricher
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    // Emitted under three claim types so role checks work regardless of which
    // one a consumer inspects: ASP.NET Identity's ClaimTypes.Role, the OIDC
    // standard "role", and the Azure AD-style "roles".
    private static readonly string[] RoleClaimTypes =
    [
        ClaimTypes.Role,
        "role",
        "roles",
    ];

    // Default EPiServer editor roles. "Administrators" is included so the
    // token also works against content trees that use it in the ACL.
    private static readonly string[] EditorRoles =
    [
        "Administrators",
        "WebEditors",
        "WebAdmins",
        "CmsEditors",
        "CmsAdmins",
    ];

    // Runs after AccessTokenPrincipal is built (PrepareAccessTokenPrincipal)
    // but before the JWT is serialized (GenerateIdentityModelAccessToken).
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor
            .CreateBuilder<OpenIddictServerEvents.ProcessSignInContext>()
            .UseSingletonHandler<BlackbirdClientCredentialsRolesEnricher>()
            .SetOrder(OpenIddictServerHandlers.PrepareAccessTokenPrincipal.Descriptor.Order + 500)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    private readonly ILogger<BlackbirdClientCredentialsRolesEnricher> _logger;

    public BlackbirdClientCredentialsRolesEnricher(
        ILogger<BlackbirdClientCredentialsRolesEnricher> logger)
    {
        _logger = logger;
    }

    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        // Only touch client_credentials tokens; leave user tokens alone.
        if (context.Request?.GrantType != OpenIddictConstants.GrantTypes.ClientCredentials)
            return default;

        if (context.AccessTokenPrincipal?.Identity is not ClaimsIdentity identity)
            return default;

        // Optional: scope this to the Blackbird client only. Comment out to
        // apply to every client_credentials application you register.
        if (context.Request.ClientId != "blackbird-integration")
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
            "Blackbird: injected editor roles into client_credentials token for client={ClientId}",
            context.Request.ClientId);

        return default;
    }
}
```

##### 2. Register the handler in `ConfigureServices`

Wire the handler up alongside your existing OpenIddict configuration:

```csharp
services.AddOpenIddict()
    .AddServer(builder =>
        builder.AddEventHandler(BlackbirdClientCredentialsRolesEnricher.Descriptor));
```

That is the only registration required. `UseSingletonHandler<T>()` in the descriptor already registers the handler as a singleton in the DI container, so you do **not** need a separate `services.AddSingleton<...>()` call.

##### 3. Confirm the roles work with your ACLs

The five roles injected above (`Administrators`, `WebEditors`, `WebAdmins`, `CmsEditors`, `CmsAdmins`) cover the default EPiServer editor roles used by the Alloy sample and most standard installations. In the CMS admin UI (**Admin → Access Rights**), verify that at least one of those roles has **Read, Create, Change, and Publish** on the content tree Blackbird will translate (and that the permissions apply to sub-items). No further ACL changes are needed if your site already uses these default roles.

If your installation uses a custom editor role name instead, either add that name to `EditorRoles` in the handler above, or grant one of the default roles the required permissions on the target content tree.

##### Why not just use `IClaimsTransformation`?

A runtime `IClaimsTransformation` looks like the obvious fix — decorate the principal with role claims when it is authenticated — but it is unreliable for the Content Management API. Inside the request pipeline, EPiServer re-authenticates the bearer token multiple times (once for the endpoint's authorization policy, and again when the controller reads the current principal). Each `AuthenticateAsync` call re-parses the JWT into a fresh `ClaimsIdentity`, so a claim added on one pass is not guaranteed to be present on the pass the ACL evaluator actually inspects — you get intermittent `403`s. Baking the roles into the JWT during token issuance sidesteps all of this: every downstream authenticate produces an identity with the roles already on it.

#### Values to hand over to Blackbird

After deploying this change, give Blackbird:

- `Base URL` → your Optimizely instance URL
- `Client ID` → the `ClientId` you set above (`blackbird-integration`)
- `Client secret` → the `ClientSecret` you set above
- `Username` / `Password` → only for the "Username & password" connection type: a CMS account that is allowed to use the management API

> **Note:** the exact property names can vary slightly between versions of the `EPiServer.OpenIDConnect` and `EPiServer.ContentManagementApi` packages, so adjust to match your installed version. Keep the Client secret out of source control (use environment variables or a secret store).

### Does this app work if my Optimizely uses Opti ID?

Yes. Opti ID is Optimizely's single sign-on for **people** — it changes how your editors log in to the CMS in the browser, but it does not provide credentials for integrations like Blackbird, and nothing about it needs to change.

If your instance uses Opti ID, keep the following in mind:

- You cannot connect Blackbird with an Opti ID account. Opti ID has no application (machine-to-machine) login, so the app authenticates against the CMS API directly, using the Client ID and Client secret registration described above. Nothing is configured in Opti ID or its Admin Center.
- Editors who sign in through Opti ID usually stop having local CMS passwords, so use the **Client credentials** connection type (see _Connecting_ below). It only needs the Base URL, Client ID, and Client secret — no CMS username or password. You must also apply the OpenIddict handler shown in the "Extra step for the Client credentials connection type" section above; without it, every Content Management API call will fail with `403 Forbidden`.
- Opti ID must not be your site's only authentication method (your developer should register it with `useAsDefault: false`, which is the standard setup), so the API login endpoint this app uses stays available.

In short: Opti ID handles your editors, this app keeps using the classic API credentials — the two work side by side.

## Connecting

The app offers two connection types. Pick the one that matches your Optimizely setup:

- **Username & password** — the app signs in as a CMS user. Use this when your instance has local CMS accounts (ASP.NET Identity).
- **Client credentials** — the app signs in as an application, with no user account. Use this when your editors log in through Opti ID or another SSO and local CMS passwords are not available. Your developer's registration must include the Content Management API scope (as in the example above), and the OpenIddict handler from the "Extra step for the Client credentials connection type" section must be installed so the issued token carries editor roles — otherwise every content operation fails with `403 Forbidden`. The `AllowResourceOwnerPasswordFlow` line is not needed for this type.

1. Navigate to Apps and search for `Optimizely`.
2. Click _Add connection_.
3. Name the connection for future reference.
4. Choose the connection type and fill in the fields:
   - _Username & password_: `Base URL` (for example `https://localhost:5000`), `Client ID`, `Client secret`, `Username`, `Password`
   - _Client credentials_: `Base URL`, `Client ID`, `Client secret`
5. Save the connection.

The app validates the connection by requesting an access token from `/api/episerver/connect/token`.

## Actions

### Content

- **Search content** walks the content tree below the selected root content ID and filters the returned nodes in memory by GUID, content type, name, category, locale, publish window, and publication status. You can also cap the traversal with `Max depth` and `Max results`.
- **Download content** downloads the selected content item from `/api/episerver/v3.0/contentmanagement/{contentId}` and converts the selected localizable fields into a Blackbird interoperable HTML file. You can also choose reference fields so the linked content entries are embedded into the exported file.
- **Upload content** accepts a translated `.html`, `.xlf`, or `.xliff` file and patches the selected language variant in Optimizely. Reference entries are updated after the main content item. If a reference update fails, the action returns a partial-failure result instead of throwing for the entire upload.

### Languages

- **Search languages** lists the languages available in the Optimizely site configuration. This is useful for validating language setup and for debugging localization flows.

## Feedback

Do you want to use this app or do you have feedback on our implementation? Reach out to us using the [established channels](https://www.blackbird.io/) or create an issue.

<!-- end docs -->
