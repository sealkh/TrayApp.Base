namespace TrayApp.Base.Updates;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task ApplyUpdateAsync(UpdateCheckResult update, CancellationToken cancellationToken = default);
}
