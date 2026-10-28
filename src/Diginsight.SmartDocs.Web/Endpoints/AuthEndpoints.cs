using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

namespace Diginsight.SmartDocs.Web.Endpoints;

/// <summary>
/// Whether this host signs readers in. Sign-in is wired only when <c>AzureAd:ClientId</c> is set;
/// otherwise the host stays anonymous-only and none of the authentication services exist.
/// </summary>
public sealed record SignInConfiguration(bool Enabled)
{
    /// <summary>The claim type Entra ID uses for app role values.</summary>
    public const string RolesClaimType = RoleSet.RolesClaimType;

    public static SignInConfiguration From(IConfiguration configuration) =>
        new(!string.IsNullOrWhiteSpace(configuration["AzureAd:ClientId"]));
}

/// <summary>
/// Sign-in inside the host: Microsoft Identity Web with an authentication cookie, and the two
/// endpoints the shell links to. No fallback authorization policy is registered, so anonymous
/// reading keeps working; what a reader sees is decided by <c>ReaderAccess</c> alone.
/// </summary>
public static class AuthEndpoints
{
    public const string SignInPath = "/_auth/signin";
    public const string SignOutPath = "/_auth/signout";

    /// <summary>
    /// Registers OpenID Connect sign-in and the session cookie when <paramref name="signIn"/> is
    /// enabled, and nothing otherwise.
    /// </summary>
    public static IServiceCollection AddReaderSignIn(
        this IServiceCollection services,
        IConfiguration configuration,
        SignInConfiguration signIn,
        SiteAuthorizationOptions authorization)
    {
        if (!signIn.Enabled)
        {
            return services;
        }

        services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(
                identity => configuration.GetSection("AzureAd").Bind(identity),
                cookie =>
                {
                    // Roles refresh only at sign-in, so the cookie's lifetime bounds how long a
                    // removed grant keeps working; sliding would extend it with every request.
                    cookie.ExpireTimeSpan = authorization.SessionLifetime;
                    cookie.SlidingExpiration = false;
                    cookie.Cookie.HttpOnly = true;
                    cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    cookie.Cookie.SameSite = SameSiteMode.Lax;
                });

        // Keep the token's claim names, so role values arrive as "roles" claims and the reader's
        // display name as "name". Post-configured so it applies after Identity Web's own defaults.
        services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters.RoleClaimType = SignInConfiguration.RolesClaimType;
            options.TokenValidationParameters.NameClaimType = "name";
            // The ID token's lifetime must not replace the configured session lifetime.
            options.UseTokenLifetime = false;
        });

        return services;
    }

    /// <summary>Maps <c>GET /_auth/signin</c> and <c>POST /_auth/signout</c> when sign-in is enabled.</summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, SignInConfiguration signIn)
    {
        if (!signIn.Enabled)
        {
            return app;
        }

        app.MapGet(SignInPath, (string? returnUrl) =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = ToLocalReturnUrl(returnUrl) },
                [OpenIdConnectDefaults.AuthenticationScheme]));

        app.MapPost(SignOutPath, async (HttpContext http, IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(http))
            {
                return Results.BadRequest();
            }

            // Signing out navigates away, which also closes the page's hub connection.
            return Results.SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
        });

        return app;
    }

    /// <summary>
    /// <paramref name="returnUrl"/> when it's a local path—one leading <c>/</c>, not <c>//</c> or
    /// <c>/\</c>—and <c>/</c> otherwise, so the sign-in endpoint can't redirect off the site.
    /// </summary>
    public static string ToLocalReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl) || returnUrl[0] != '/')
        {
            return "/";
        }

        if (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\'))
        {
            return "/";
        }

        // Control characters have no place in a path and some agents strip them, turning "/\t/x"
        // into "//x".
        return returnUrl.Any(char.IsControl) ? "/" : returnUrl;
    }
}
