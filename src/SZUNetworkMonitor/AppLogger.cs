using System.Text;

namespace SZUNetworkMonitor;

internal static class AppLogger
{
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SZUNetworkMonitor");
    public static readonly string LogPath = Path.Combine(LogDirectory, "monitor.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warning(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(LogDirectory);
            RotateIfNeeded();
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}{Environment.NewLine}";
            File.AppendAllText(LogPath, line, new UTF8Encoding(false));
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(LogPath) || new FileInfo(LogPath).Length < 1024 * 1024)
        {
            return;
        }

        File.Move(LogPath, LogPath + ".previous", true);
    }
}
