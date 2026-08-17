namespace Diginsight.SmartDocs.Web.Shared.Navigation;

/// <summary>
/// How much of a folder's subtree a computed aggregate actually observed. This is what keeps
/// "unknown" distinct from "zero": a fold that could not see every descendant yields a
/// <see cref="Partial"/> lower bound instead of a total, and a lower bound is never allowed to
/// replace a <see cref="Complete"/> value.
/// </summary>
public enum Coverage
{
    /// <summary>Nothing is known yet — render "…", never 0.</summary>
    None = 0,

    /// <summary>Some descendants contributed; unknown ones contributed nothing. A monotonic lower bound — render "≥ N".</summary>
    Partial = 1,

    /// <summary>Every descendant was observed. The value is the true total.</summary>
    Complete = 2,
}
