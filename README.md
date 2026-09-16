# SZU Campus Network Monitor

> A small Windows tray app that keeps the Shenzhen University teaching-area SRun network connected.

It checks the public network every minute. Each check sends up to five pings to `www.baidu.com`; when the first ping is lost, the app immediately tries to sign in to the campus network again.

## Download and use

For most people, **do not clone this repository and do not run PowerShell commands**.

1. Open [Releases](../../releases) and download `SZUNetworkMonitor-Setup.exe`.
2. Run the installer, then open **SZU Campus Network Monitor** from the Start menu.
3. Enter your campus-network username and password once.
4. Keep **Start with Windows** enabled and save.
5. Find the app icon in the Windows system tray (look in the `^` overflow area if necessary).

That is all. The app stays in the tray after you close the settings window.

## What the tray app does

| Status | Meaning |
| --- | --- |
| `Checking network: 1/5` | A connectivity check is in progress. |
| `Network OK: 5/5 replies` | The connection is healthy. |
| `Packet loss ... Reconnecting...` | A reply was lost and the app is signing in again. |
| `Campus login succeeded` | The reconnect request completed successfully. |
| `Campus login failed` | Open the log from the tray menu for the error detail. |

Right-click the tray icon to:

- run an immediate check;
- edit the account or password;
- turn Windows startup on or off;
- open the local log;
- exit the monitor.

Double-clicking the icon also runs an immediate check.

## Privacy and security

- The password is stored only on this PC, under `%LOCALAPPDATA%\SZUNetworkMonitor\settings.json`.
- Windows DPAPI encrypts it for the current Windows user. Copying that file to another Windows account does not reveal the password.
- The app never prints the password or puts it on a command line. It passes the password to its local login helper over standard input.
- Logs contain connection states and safe error messages only. They are available from the tray menu.

## Why no `hosts` file or administrator prompt?

The original command-line login tool first resolves `net.szu.edu.cn`. During a campus-network outage, DNS can be unavailable too, which makes a reconnect fail before authentication starts.

This app packages a modified local helper that connects to the configured authentication IP directly while retaining HTTPS certificate verification for `net.szu.edu.cn`. It does **not** edit the Windows `hosts` file and does not need administrator privileges.

## Troubleshooting

### The tray icon is missing

Check the `^` menu next to the Windows clock. Windows may place newly installed tray icons there. You can make it always visible in Windows taskbar settings.

### Reconnect keeps failing

Use **Open log** in the tray menu. Confirm that:

1. You are connected to the SZU teaching-area Wi-Fi or wired network.
2. The campus-network account and password are correct.
3. You are not on the dormitory ePortal network; this app currently supports the teaching-area SRun endpoint only.

When reporting a problem, include the log message but remove your username, IP address, and any other personal information first.

### I changed my password

Right-click the tray icon, select **Account and settings**, enter the new password, and save. No reinstall is needed.

### I do not want it to start with Windows

Right-click the tray icon and uncheck **Start with Windows**. The app writes only a current-user startup entry, so it does not need administrator rights.

## Build from source

This section is for maintainers, not normal users.

Requirements:

- Windows 10 or later, x64
- .NET 8 SDK
- Go 1.22 or later
- Inno Setup 6 (optional; only required for the installer)

Build a self-contained portable application and, when Inno Setup is installed, an installer:

```powershell
.\build.ps1
```

Outputs are written to `artifacts/`:

- `artifacts/publish/` - portable x64 application;
- `artifacts/SZUNetworkMonitor-Setup.exe` - installer, when Inno Setup is available.

## Release process

GitHub Actions builds the installer and a portable zip on every version tag.

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The workflow publishes `SZUNetworkMonitor-Setup.exe` and `SZUNetworkMonitor-portable-win-x64.zip` to GitHub Releases.

## License and attribution

The local SRun helper is a modified derivative of [nnothing1/szu-srun-login](https://github.com/nnothing1/szu-srun-login), which is licensed under AGPL-3.0. This project is distributed under AGPL-3.0-or-later. See [LICENSE](LICENSE) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
