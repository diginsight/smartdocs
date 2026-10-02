using Diginsight.Diagnostics;
using System.Diagnostics;
using System.Reflection;

namespace Diginsight.SmartDocs.Web;

internal static class Observability
{
    public static readonly ActivitySource ActivitySource = new(Assembly.GetExecutingAssembly().GetName().Name!);

    /// <summary>
    /// Activities started once per cache lookup, per file read, or per menu level — the ones whose
    /// count grows with the work a single request does rather than with the number of requests.
    /// </summary>
    /// <remarks>
    /// They are a separate source so that a deployed instance can switch them off and keep the
    /// per-request ones, which the single source this application used could not express. On the
    /// profile a deployed instance runs, the application's own activities cost about 43% of a
    /// request and their records another 16%; nearly all of that is started here. See
    /// <c>src/docs/90.00-issues/202610/20261001.01-perfanalysis/01-startup-and-navigation-optimization.analysis.md</c>,
    /// change <c>C2-trim-hot-path-activities</c>.
    /// <para>
    /// A wildcard <c>false</c> vetoes a more specific <c>true</c>, so this source is named under the
    /// assembly's own so that gating it never gates the per-request ones with it, and the base
    /// settings list it explicitly as <c>false</c> while the Development overlay lists it as
    /// <c>true</c>.
    /// </para>
    /// </remarks>
    public static readonly ActivitySource HotPathActivitySource =
        new(Assembly.GetExecutingAssembly().GetName().Name! + ".HotPath");

    public static ILoggerFactory? LoggerFactory => LoggerFactoryStaticAccessor.LoggerFactory;
}
