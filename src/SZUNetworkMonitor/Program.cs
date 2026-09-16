namespace SZUNetworkMonitor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var instanceLease = SingleInstanceLease.TryAcquireForCurrentUser();
        if (instanceLease is null)
        {
            AppLogger.Info("A monitor instance is already running for this user; the duplicate process is exiting.");
            return;
        }

        AppLogger.Info("Monitor process started.");
        Application.Run(new MonitorApplicationContext());
    }
}
