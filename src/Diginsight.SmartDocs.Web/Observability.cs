using Diginsight.Diagnostics;
using System.Diagnostics;
using System.Reflection;

namespace Diginsight.SmartDocs.Web;

internal static class Observability
{
    public static readonly ActivitySource ActivitySource = new(Assembly.GetExecutingAssembly().GetName().Name!);

    /// <summary>
    /// Legacy activity source retained for compatibility with existing configuration overlays.
    /// </summary>
    /// <remarks>
    /// Server instrumentation now uses <see cref="ActivitySource"/> consistently. The legacy
    /// HotPath switch no longer suppresses per-lookup spans; control their volume through the
    /// normal source, logging filters, and sampling.
    /// </remarks>
    public static readonly ActivitySource HotPathActivitySource =
        new(Assembly.GetExecutingAssembly().GetName().Name! + ".HotPath");

    public static ILoggerFactory? LoggerFactory => LoggerFactoryStaticAccessor.LoggerFactory;
}
