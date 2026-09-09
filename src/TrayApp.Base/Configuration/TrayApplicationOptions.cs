namespace TrayApp.Base.Configuration;
using System.Globalization;
public class TrayApplicationOptions {
    public string ApplicationName { get; set; } = "Tray Application";
    public string Tooltip { get; set; } = "Tray Application";
    public string? SingleInstanceId { get; set; }
    public CultureInfo Culture { get; set; } = CultureInfo.CurrentUICulture;
    public bool AutomatedUpstreamDeployment { get; set; }
    public bool ValidateVersionOnDemand { get; set; } = true;
    public string? IconPath { get; set; }
}