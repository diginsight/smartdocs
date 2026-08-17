using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.AspNetCore.Components;

namespace Diginsight.SmartDocs.Web.Client.Layout;

public partial class TopMenuDropdown
{
    [Parameter] public IReadOnlyList<NavChild> Nodes { get; set; } = Array.Empty<NavChild>();
}
