using System.Globalization;

namespace TrayApp.Base.Configuration;

public sealed class TrayApplicationOptions
{
    public string ApplicationName { get; init; } = "Tray Application";

    public string Tooltip { get; init; } = "Tray Application";

    public string? SingleInstanceId { get; init; }

    public CultureInfo Culture { get; init; } = CultureInfo.CurrentUICulture;

    public bool AutomatedUpstreamDeployment { get; init; }

    public bool ValidateVersionOnDemand { get; init; } = true;
}
