using System.Security.Claims;
using Diginsight.SmartDocs.Authorization;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Diginsight.SmartDocs.Web.Authorization;

public sealed class RoleProviderSignInHook(
    IRoleProvider provider,
    RoleProviderOptions options,
    IOptions<SiteOptions> site,
    ILogger<RoleProviderSignInHook> logger)
{
    public void Configure(OpenIdConnectOptions oidc)
    {
        Func<TicketReceivedContext, Task> previous = oidc.Events.OnTicketReceived;
        oidc.Events.OnTicketReceived = async context =>
        {
            await previous(context);
            if (context.Result is not null)
            {
                return;
            }

            await ResolveAsync(context);
        };
    }

    private async Task ResolveAsync(TicketReceivedContext context)
    {
        if (context.Principal?.Identities.Any(static i => i.IsAuthenticated) != true)
        {
            context.Fail("The role provider requires an authenticated principal.");
            return;
        }

        ClaimsPrincipal original = Clone(context.Principal);
        ClaimsPrincipal clean = Clone(context.Principal);
        foreach (ClaimsIdentity identity in clean.Identities)
        {
            foreach (Claim claim in identity.Claims.Where(c => c.Type == RoleSet.RolesClaimType ||
                c.Type == ClaimTypes.Role || c.Type == identity.RoleClaimType || c.Type == RoleSet.ScopedRolesClaimType).ToArray())
            {
                identity.RemoveClaim(claim);
            }
        }
        context.Principal = clean;

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.HttpContext.RequestAborted);
        cancellation.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            Task<ResolvedRoles> task = provider.ResolveAsync(original, cancellation.Token);
            // Observe a late fault even if a provider ignores its cancellation token.
            _ = task.ContinueWith(t => logger.LogWarning("A role provider task failed ({FailureType})",
                    t.Exception!.GetBaseException().GetType().Name),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            ResolvedRoles resolved = await task.WaitAsync(TimeSpan.FromSeconds(options.TimeoutSeconds), context.HttpContext.RequestAborted);
            var scopes = site.Value.Spaces.Select(static s => s.RoleScope?.Trim())
                .Where(static s => !string.IsNullOrEmpty(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var claims = new List<Claim>();
            foreach (string role in resolved.Global)
            {
                string value = role?.Trim() ?? "";
                if (AccessFile.IsValidRoleName(value))
                {
                    claims.Add(new Claim(RoleSet.RolesClaimType, value));
                }
                else
                {
                    logger.LogWarning("Dropped provider global role {Role} with an invalid name", value);
                }
            }

            var scoped = new SortedSet<string>(StringComparer.Ordinal);
            foreach (ScopedRole assignment in resolved.Scoped)
            {
                string role = assignment.Role?.Trim() ?? "";
                string scope = assignment.Scope?.Trim() ?? "";
                if (!AccessFile.IsValidRoleName(role) || scope.Length == 0 || assignment.Scope?.Any(char.IsControl) == true || !scopes.Contains(scope))
                {
                    logger.LogWarning("Dropped provider scoped role {Role}@{Scope} with an invalid name or unconfigured scope", role, scope);
                    continue;
                }
                scoped.Add($"{role.ToUpperInvariant()}@{scope.ToUpperInvariant()}");
            }
            if (scoped.Count > options.MaxScopedRoles)
            {
                logger.LogWarning("Truncated {Count} scoped roles to MaxScopedRoles {MaxScopedRoles}", scoped.Count, options.MaxScopedRoles);
            }
            claims.AddRange(scoped.Take(options.MaxScopedRoles).Select(static value => new Claim(RoleSet.ScopedRolesClaimType, value)));
            ClaimsIdentity target = clean.Identities.First(static i => i.IsAuthenticated);
            target.AddClaims(claims);
        }
        catch (Exception ex) when (!context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            cancellation.Cancel();
            logger.LogWarning("Role resolution failed ({FailureType}); issuing an authenticated-only degraded session", ex.GetType().Name);
            DateTimeOffset expires = DateTimeOffset.UtcNow + options.DegradedSessionLifetime;
            AuthenticationProperties properties = context.Properties ??= new();
            if (properties.ExpiresUtc is not { } current || current > expires)
            {
                properties.ExpiresUtc = expires;
            }
        }
    }

    private static ClaimsPrincipal Clone(ClaimsPrincipal principal) =>
        new(principal.Identities.Select(static identity => identity.Clone()));
}
