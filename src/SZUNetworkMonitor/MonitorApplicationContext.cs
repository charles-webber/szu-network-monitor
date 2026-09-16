using System.Diagnostics;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace SZUNetworkMonitor;

internal sealed class MonitorApplicationContext : ApplicationContext
{
    private const string ApplicationName = "SZU Network Monitor";
    private const int PollIntervalMilliseconds = 60_000;
    private const int MaximumPingCount = 5;
    private readonly SettingsStore _settingsStore = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _startupMenuItem;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly CancellationTokenSource _shutdownCancellation = new();
    private readonly AuthenticationAttemptGate _authenticationGate = new(TimeProvider.System, TimeSpan.FromMinutes(1));
    private AppSettings? _settings;
    private SetupForm? _settingsForm;
    private Task? _activeCheck;
    private Task? _exitTask;
    private bool _monitoringStarted;
    private bool _isExiting;

    public MonitorApplicationContext()
    {
        _statusMenuItem = new ToolStripMenuItem("正在启动…") { Enabled = false };
        _startupMenuItem = new ToolStripMenuItem("开机自启") { CheckOnClick = true };
        _startupMenuItem.Click += StartupMenuItemClick;

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("立即检查", null, (_, _) => QueueCheck()));
        menu.Items.Add(new ToolStripMenuItem("账号和设置", null, (_, _) => ShowSettings()));
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(new ToolStripMenuItem("打开日志", null, (_, _) => OpenLog()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitApplication()));

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = ApplicationName,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => QueueCheck();

        _pollTimer = new System.Windows.Forms.Timer { Interval = PollIntervalMilliseconds };
        _pollTimer.Tick += (_, _) => QueueCheck();

        _settings = _settingsStore.Load();
        _startupMenuItem.Checked = StartupManager.IsEnabled();

        if (_settings?.IsConfigured == true)
        {
            StartMonitoring();
        }
        else
        {
            UpdateStatus("需要完成首次设置", ToolTipIcon.Warning);
            ShowSettings();
        }
    }

    private void StartMonitoring()
    {
        if (_monitoringStarted || _isExiting)
        {
            return;
        }

        _monitoringStarted = true;
        _pollTimer.Start();
        QueueCheck();
    }

    private void QueueCheck()
    {
        if (_isExiting || _settings?.IsConfigured != true || _activeCheck is { IsCompleted: false })
        {
            return;
        }

        // There is exactly one timer and one queued monitor task. The timer is
        // restarted only after that task completes, so ticks cannot overlap.
        _pollTimer.Stop();
        _activeCheck = RunCheckAsync(_shutdownCancellation.Token);
    }

    private async Task RunCheckAsync(CancellationToken cancellationToken)
    {
        var settings = _settings;
        if (settings?.IsConfigured != true)
        {
            return;
        }

        var pingCount = Math.Clamp(settings.PingCount, 1, MaximumPingCount);
        try
        {
            var successfulPings = 0;
            using var pinger = new Ping();
            for (var attempt = 1; attempt <= pingCount; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                UpdateStatus($"正在检测 {attempt}/{pingCount}", ToolTipIcon.Info);
                PingReply reply;
                try
                {
                    reply = await pinger.SendPingAsync("www.baidu.com", settings.PingTimeoutMilliseconds).WaitAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    AppLogger.Warning($"Ping {attempt}/{pingCount} failed: {exception.Message}");
                    await ReconnectAsync(attempt, successfulPings, "Ping request failed.", cancellationToken);
                    return;
                }

                if (reply.Status != IPStatus.Success)
                {
                    AppLogger.Warning($"Ping {attempt}/{pingCount} failed with {reply.Status}.");
                    await ReconnectAsync(attempt, successfulPings, reply.Status.ToString(), cancellationToken);
                    return;
                }

                successfulPings++;
            }

            AppLogger.Info($"Connectivity check passed ({successfulPings}/{pingCount} replies).");
            UpdateStatus($"网络正常：{successfulPings}/{pingCount}，1 分钟后再次检查", ToolTipIcon.Info);
        }
        catch (OperationCanceledException) when (_shutdownCancellation.IsCancellationRequested)
        {
            AppLogger.Info("Monitor check cancelled during shutdown.");
        }
        catch (Exception exception)
        {
            AppLogger.Error($"Monitor check failed: {exception.Message}");
            UpdateStatus("监控错误：请打开日志查看", ToolTipIcon.Error);
            _notifyIcon.ShowBalloonTip(4000, ApplicationName, "网络监控发生错误，请打开日志查看。", ToolTipIcon.Error);
        }
        finally
        {
            if (!_isExiting && _monitoringStarted && _settings?.IsConfigured == true)
            {
                _pollTimer.Start();
            }
        }
    }

    private async Task ReconnectAsync(int failedAttempt, int successfulPings, string reason, CancellationToken cancellationToken)
    {
        var attemptState = _authenticationGate.TryBegin(out var remainingCooldown);
        if (attemptState == AuthenticationAttemptState.AlreadyRunning)
        {
            AppLogger.Warning("Reconnect skipped because an authentication attempt is already running.");
            UpdateStatus("认证任务正在运行", ToolTipIcon.Warning);
            return;
        }
        if (attemptState == AuthenticationAttemptState.CoolingDown)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(remainingCooldown.TotalSeconds));
            AppLogger.Warning($"Reconnect skipped by the login failure cooldown ({seconds} seconds remaining).");
            UpdateStatus($"认证冷却中：{seconds} 秒后重试", ToolTipIcon.Warning);
            return;
        }

        var succeeded = false;
        try
        {
            UpdateStatus("正在重连", ToolTipIcon.Warning);
            AppLogger.Warning($"Packet loss on ping {failedAttempt}; reconnecting. Reason: {reason}");

            string password;
            try
            {
                password = _settingsStore.ReadPassword(_settings!);
            }
            catch (Exception exception)
            {
                AppLogger.Error($"Unable to decrypt stored password: {exception.Message}");
                UpdateStatus("重连失败：无法读取已保存密码", ToolTipIcon.Error);
                return;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            UpdateStatus("正在发现门户", ToolTipIcon.Info);
            var result = await LoginClient.LoginAsync(_settings!.Username, password, timeout.Token);
            if (result.Success)
            {
                succeeded = true;
                AppLogger.Info($"Campus login succeeded: {result.Message}");
                UpdateStatus("重连成功", ToolTipIcon.Info);
                _notifyIcon.ShowBalloonTip(3000, ApplicationName, "校园网重连成功。", ToolTipIcon.Info);
                return;
            }

            var safeReason = LoginClient.ToUserFacingFailure(result);
            AppLogger.Error($"Campus login failed: {result.Error ?? result.Message}");
            UpdateStatus($"重连失败：{safeReason}", ToolTipIcon.Error);
            _notifyIcon.ShowBalloonTip(4000, ApplicationName, $"校园网重连失败：{safeReason}", ToolTipIcon.Error);
        }
        catch (OperationCanceledException) when (_shutdownCancellation.IsCancellationRequested)
        {
            AppLogger.Info("Campus login cancelled during shutdown.");
        }
        catch (Exception exception)
        {
            AppLogger.Error($"Reconnect failed unexpectedly: {exception.Message}");
            UpdateStatus("重连失败：认证过程异常", ToolTipIcon.Error);
        }
        finally
        {
            _authenticationGate.Complete(succeeded);
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
            MessageBox.Show("保存设置失败：" + exception.Message, ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            MessageBox.Show("无法更新自启设置：" + exception.Message, ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateStatus(string status, ToolTipIcon icon)
    {
        _statusMenuItem.Text = status;
        _notifyIcon.Text = status.Length <= 63 ? status : status[..60] + "…";
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
            MessageBox.Show("无法打开日志：" + exception.Message, ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExitApplication()
    {
        _exitTask ??= ExitApplicationAsync();
    }

    private async Task ExitApplicationAsync()
    {
        _isExiting = true;
        _monitoringStarted = false;
        _pollTimer.Stop();
        _shutdownCancellation.Cancel();

        var activeCheck = _activeCheck;
        if (activeCheck is not null)
        {
            try
            {
                await activeCheck;
            }
            catch (OperationCanceledException)
            {
                // The cancellation is the expected shutdown path.
            }
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _isExiting = true;
        _pollTimer.Stop();
        _shutdownCancellation.Cancel();
        _pollTimer.Dispose();
        _shutdownCancellation.Dispose();
        base.ExitThreadCore();
    }
}
