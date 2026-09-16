using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SZUNetworkMonitor;

internal sealed class AppSettings
{
    public string Username { get; set; } = string.Empty;
    public string ProtectedPassword { get; set; } = string.Empty;
    public bool StartWithWindows { get; set; } = true;
    public int PollingSeconds { get; set; } = 60;
    public int PingCount { get; set; } = 5;
    public int PingTimeoutMilliseconds { get; set; } = 3000;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(ProtectedPassword);
}

internal sealed class SettingsStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SZU-Network-Monitor-v1");
    private readonly string _settingsPath;

    public SettingsStore()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SZUNetworkMonitor");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public AppSettings? Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath));
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            AppLogger.Error($"Unable to read settings: {exception.Message}");
            return null;
        }
    }

    public string ReadPassword(AppSettings settings)
    {
        var encrypted = Convert.FromBase64String(settings.ProtectedPassword);
        var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(decrypted);
    }

    public AppSettings Create(string username, string password, bool startWithWindows)
    {
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.CurrentUser);
        return new AppSettings
        {
            Username = username.Trim(),
            ProtectedPassword = Convert.ToBase64String(protectedBytes),
            StartWithWindows = startWithWindows
        };
    }

    public void Save(AppSettings settings)
    {
        var temporaryPath = _settingsPath + ".tmp";
        var content = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));
        File.Move(temporaryPath, _settingsPath, true);
    }
}
