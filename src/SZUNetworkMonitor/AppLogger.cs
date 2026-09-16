using System.Text;
using System.Text.RegularExpressions;

namespace SZUNetworkMonitor;

internal static class AppLogger
{
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SZUNetworkMonitor");
    private static readonly Regex IPv4Address = new(@"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])", RegexOptions.CultureInvariant);
    private static readonly Regex MacAddress = new(@"(?i)(?<![0-9a-f])(?:[0-9a-f]{2}[:-]){5}[0-9a-f]{2}(?![0-9a-f])", RegexOptions.CultureInvariant);
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
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [PID {Environment.ProcessId}] [{level}] {RedactNetworkIdentifiers(message)}{Environment.NewLine}";
            File.AppendAllText(LogPath, line, new UTF8Encoding(false));
        }
    }

    private static string RedactNetworkIdentifiers(string message)
    {
        var withoutMac = MacAddress.Replace(message, "[MAC 已隐藏]");
        return IPv4Address.Replace(withoutMac, "[IP 已隐藏]");
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
