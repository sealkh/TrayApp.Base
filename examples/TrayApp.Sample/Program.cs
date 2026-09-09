using TrayApp.Base;
using TrayApp.Base.Configuration;
return new SampleTrayApplication().Run();
internal sealed class SampleTrayApplication : TrayApplication {
    public SampleTrayApplication() : base(new TrayApplicationOptions {
        ApplicationName = "TrayApp.Sample",
        Tooltip = "TrayApp.Sample",
        SingleInstanceId = "Local\\TrayApp.Sample",
        IconPath = "Assets\\TrayApp.Sample.ico"
    }) { }
    protected override void OnOpen() {
        // Open or restore your status/configuration window here.
    }
    protected override void OnSettings() {
        // Open application settings here.
    }
}