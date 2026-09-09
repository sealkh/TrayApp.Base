namespace TrayApp.Base.Updates;

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    Version? CurrentVersion = null,
    Version? AvailableVersion = null,
    string? ReleaseNotes = null);
