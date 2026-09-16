using System.Diagnostics;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace SZUNetworkMonitor;

internal sealed class MonitorApplicationContext : ApplicationContext
{
    private const string ApplicationName = "SZU Network Monitor";
    private readonly SettingsStore _settingsStore = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _startupMenuItem;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private AppSettings? _settings;
    private SetupForm? _settingsForm;
    private bool _isChecking;
    private bool _isExiting;

    public MonitorApplicationContext()
    {
        _statusMenuItem = new ToolStripMenuItem("\u6b63\u5728\u542f\u52a8...") { Enabled = false };
        _startupMenuItem = new ToolStripMenuItem("\u5f00\u673a\u81ea\u542f") { CheckOnClick = true };
        _startupMenuItem.Click += StartupMenuItemClick;

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("\u7acb\u5373\u68c0\u67e5", null, async (_, _) => await RunCheckAsync()));
        menu.Items.Add(new ToolStripMenuItem("\u8d26\u53f7\u548c\u8bbe\u7f6e", null, (_, _) => ShowSettings()));
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(new ToolStripMenuItem("\u6253\u5f00\u65e5\u5fd7", null, (_, _) => OpenLog()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("\u9000\u51fa", null, (_, _) => ExitApplication()));

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = ApplicationName,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += async (_, _) => await RunCheckAsync();

        _pollTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _pollTimer.Tick += async (_, _) => await RunCheckAsync();

        _settings = _settingsStore.Load();
        _startupMenuItem.Checked = StartupManager.IsEnabled();

        if (_settings?.IsConfigured == true)
        {
            StartMonitoring();
        }
        else
        {
            UpdateStatus("\u9700\u8981\u5b8c\u6210\u9996\u6b21\u8bbe\u7f6e", ToolTipIcon.Warning);
            ShowSettings();
        }
    }

    private void StartMonitoring()
    {
        _pollTimer.Start();
        _ = RunCheckAsync();
    }

    private async Task RunCheckAsync()
    {
        if (_isChecking || _settings?.IsConfigured != true)
        {
            return;
        }

        _isChecking = true;
        _pollTimer.Stop();
        try
        {
            var successfulPings = 0;
            using var pinger = new Ping();
            for (var attempt = 1; attempt <= _settings.PingCount; attempt++)
            {
                UpdateStatus($"\u6b63\u5728\u68c0\u6d4b\u7f51\u7edc: {attempt}/{_settings.PingCount}", ToolTipIcon.Info);
                PingReply reply;
                try
                {
                    reply = await pinger.SendPingAsync("www.baidu.com", _settings.PingTimeoutMilliseconds);
                }
                catch (Exception exception)
                {
                    AppLogger.Warning($"Ping {attempt}/{_settings.PingCount} failed: {exception.Message}");
                    await ReconnectAsync(attempt, successfulPings, "Ping request failed.");
                    return;
                }

                if (reply.Status != IPStatus.Success)
                {
                    AppLogger.Warning($"Ping {attempt}/{_settings.PingCount} failed with {reply.Status}.");
                    await ReconnectAsync(attempt, successfulPings, reply.Status.ToString());
                    return;
                }

                successfulPings++;
            }

            var normalStatus = $"\u7f51\u7edc\u6b63\u5e38: {successfulPings}/{_settings.PingCount} Ping \u6210\u529f; 1 \u5206\u949f\u540e\u518d\u6b21\u68c0\u67e5";
            AppLogger.Info($"Connectivity check passed ({successfulPings}/{_settings.PingCount} replies).");
            UpdateStatus(normalStatus, ToolTipIcon.Info);
        }
        catch (Exception exception)
        {
            AppLogger.Error($"Monitor check failed: {exception.Message}");
            UpdateStatus("\u76d1\u63a7\u9519\u8bef: \u8bf7\u6253\u5f00\u65e5\u5fd7\u67e5\u770b", ToolTipIcon.Error);
            _notifyIcon.ShowBalloonTip(4000, ApplicationName, "\u7f51\u7edc\u76d1\u63a7\u53d1\u751f\u9519\u8bef\uff0c\u8bf7\u6253\u5f00\u65e5\u5fd7\u67e5\u770b\u3002", ToolTipIcon.Error);
        }
        finally
        {
            _isChecking = false;
            if (!_isExiting && _settings?.IsConfigured == true)
            {
                _pollTimer.Start();
            }
        }
    }

    private async Task ReconnectAsync(int failedAttempt, int successfulPings, string reason)
    {
        UpdateStatus($"\u68c0\u6d4b\u5230\u4e22\u5305 ({successfulPings}/{_settings!.PingCount})\uff0c\u6b63\u5728\u91cd\u8fde...", ToolTipIcon.Warning);
        AppLogger.Warning($"Packet loss on ping {failedAttempt}; reconnecting. Reason: {reason}");

        string password;
        try
        {
            password = _settingsStore.ReadPassword(_settings);
        }
        catch (Exception exception)
        {
            AppLogger.Error($"Unable to decrypt stored password: {exception.Message}");
            UpdateStatus("\u65e0\u6cd5\u8bfb\u53d6\u5df2\u4fdd\u5b58\u5bc6\u7801\uff0c\u8bf7\u91cd\u65b0\u8bbe\u7f6e", ToolTipIcon.Error);
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        UpdateStatus("\u6b63\u5728\u8fde\u63a5\u6821\u56ed\u7f51...", ToolTipIcon.Warning);
        var result = await LoginClient.LoginAsync(_settings.Username, password, timeout.Token);
        if (result.Success)
        {
            AppLogger.Info($"Campus login succeeded: {result.Message}");
            UpdateStatus("\u6821\u56ed\u7f51\u91cd\u8fde\u6210\u529f", ToolTipIcon.Info);
            _notifyIcon.ShowBalloonTip(3000, ApplicationName, "\u6821\u56ed\u7f51\u91cd\u8fde\u6210\u529f\u3002", ToolTipIcon.Info);
        }
        else
        {
            AppLogger.Error($"Campus login failed: {result.Error ?? result.Message}");
            UpdateStatus("\u6821\u56ed\u7f51\u91cd\u8fde\u5931\u8d25\uff0c\u8bf7\u6253\u5f00\u65e5\u5fd7", ToolTipIcon.Error);
            _notifyIcon.ShowBalloonTip(4000, ApplicationName, "\u6821\u56ed\u7f51\u91cd\u8fde\u5931\u8d25\uff0c\u8bf7\u68c0\u67e5\u8d26\u53f7\u5bc6\u7801\u6216\u6253\u5f00\u65e5\u5fd7\u3002", ToolTipIcon.Error);
        }
    }

    private void ShowSettings()
    {
        if (_settingsForm is not null)
        {
            _settingsForm.Activate();
            return;
        }

        var currentPassword = string.Empty;
        if (_settings?.IsConfigured == true)
        {
            try
            {
                currentPassword = _settingsStore.ReadPassword(_settings);
            }
            catch (Exception exception)
            {
                AppLogger.Error($"Unable to load existing password for settings: {exception.Message}");
            }
        }

        _settingsForm = new SetupForm(_settings, currentPassword);
        _settingsForm.SettingsSaved += SaveSettings;
        _settingsForm.FormClosed += SettingsFormClosed;
        _settingsForm.Show();
    }

    private void SaveSettings(string username, string password, bool startWithWindows)
    {
        try
        {
            var settings = _settingsStore.Create(username, password, startWithWindows);
            _settingsStore.Save(settings);
            StartupManager.SetEnabled(startWithWindows);
            _settings = settings;
            _startupMenuItem.Checked = startWithWindows;
            AppLogger.Info("Settings saved.");
            StartMonitoring();
        }
        catch (Exception exception)
        {
            AppLogger.Error($"Saving settings failed: {exception.Message}");
            MessageBox.Show("\u4fdd\u5b58\u8bbe\u7f6e\u5931\u8d25\uff1a" + exception.Message, ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SettingsFormClosed(object? sender, FormClosedEventArgs eventArgs)
    {
        _settingsForm = null;
        if (_settings?.IsConfigured != true)
        {
            ExitApplication();
        }
    }

    private void StartupMenuItemClick(object? sender, EventArgs eventArgs)
    {
        try
        {
            StartupManager.SetEnabled(_startupMenuItem.Checked);
            if (_settings is not null)
            {
                _settings.StartWithWindows = _startupMenuItem.Checked;
                _settingsStore.Save(_settings);
            }
            AppLogger.Info($"Startup setting changed to {_startupMenuItem.Checked}.");
        }
        catch (Exception exception)
        {
            _startupMenuItem.Checked = !_startupMenuItem.Checked;
            AppLogger.Error($"Unable to update startup setting: {exception.Message}");
            MessageBox.Show("\u65e0\u6cd5\u66f4\u65b0\u81ea\u542f\u8bbe\u7f6e\uff1a" + exception.Message, ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateStatus(string status, ToolTipIcon icon)
    {
        _statusMenuItem.Text = status;
        _notifyIcon.Text = status.Length <= 63 ? status : status[..60] + "...";
        _notifyIcon.Icon = icon switch
        {
            ToolTipIcon.Warning => SystemIcons.Warning,
            ToolTipIcon.Error => SystemIcons.Error,
            _ => SystemIcons.Information
        };
    }

    private static void OpenLog()
    {
        try
        {
            if (!File.Exists(AppLogger.LogPath))
            {
                File.WriteAllText(AppLogger.LogPath, string.Empty);
            }
            Process.Start(new ProcessStartInfo(AppLogger.LogPath) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show("\u65e0\u6cd5\u6253\u5f00\u65e5\u5fd7\uff1a" + exception.Message, ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _pollTimer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _pollTimer.Dispose();
        base.ExitThreadCore();
    }
}
