using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.Authorization;

public sealed class RoleProviderOptions
{
    public const string Section = "Site:Authorization:RoleProvider";

    public bool IsEnabled { get; set; } = true;
    public string? Assembly { get; set; }
    public string? Type { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxScopedRoles { get; set; } = 100;
    public TimeSpan DegradedSessionLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public bool IsPlugin => IsEnabled && !string.IsNullOrWhiteSpace(Assembly);

    public void Validate(SiteAuthorizationOptions authorization, bool signInEnabled)
    {
        if (!IsPlugin)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Type))
        {
            throw new InvalidOperationException($"{Section}:Type is required when Assembly is configured.");
        }
        if (TimeoutSeconds is < 1 or > 30)
        {
            throw new InvalidOperationException($"{Section}:TimeoutSeconds must be from 1 to 30.");
        }
        if (MaxScopedRoles <= 0)
        {
            throw new InvalidOperationException($"{Section}:MaxScopedRoles must be positive.");
        }
        if (DegradedSessionLifetime <= TimeSpan.Zero || DegradedSessionLifetime > authorization.SessionLifetime)
        {
            throw new InvalidOperationException($"{Section}:DegradedSessionLifetime must be positive and no longer than SessionLifetime.");
        }
        if (!signInEnabled)
        {
            throw new InvalidOperationException($"{Section}:Assembly requires reader sign-in (AzureAd:ClientId).");
        }
    }
}
