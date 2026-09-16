# SZU Campus Network Monitor

> A small Windows tray app that keeps the Shenzhen University teaching-area SRun network connected.

It checks the public network every minute. Each check sends up to five pings to `www.baidu.com`; when the first ping is lost, the app immediately tries to sign in to the campus network again.

## &#x4E2D;&#x6587;&#x8BF4;&#x660E;

1. &#x4E0B;&#x8F7D; [Releases](../../releases) &#x9875;&#x9762;&#x7684; `SZUNetworkMonitor-Setup.exe` &#x5E76;&#x5B89;&#x88C5;&#x3002;
2. &#x9996;&#x6B21;&#x8FD0;&#x884C;&#x65F6;&#xFF0C;&#x8F93;&#x5165;&#x6821;&#x56ED;&#x7F51;&#x8D26;&#x53F7;&#x548C;&#x5BC6;&#x7801;&#x3002;
3. &#x7A0B;&#x5E8F;&#x5728;&#x53F3;&#x4E0B;&#x89D2;&#x7CFB;&#x7EDF;&#x6258;&#x76D8;&#x8FD0;&#x884C;&#xFF1A;&#x6BCF;&#x5206;&#x949F;&#x68C0;&#x6D4B;&#x4E00;&#x6B21;&#x7F51;&#x7EDC;&#xFF0C;&#x6700;&#x591A;&#x53D1;&#x9001; 5 &#x4E2A; Ping &#x5305;&#x3002;&#x4EFB;&#x610F;&#x4E00;&#x4E2A;&#x4E22;&#x5305;&#x4F1A;&#x7ACB;&#x5373;&#x91CD;&#x8FDE;&#x6821;&#x56ED;&#x7F51;&#x3002;
4. &#x53F3;&#x952E;&#x6258;&#x76D8;&#x56FE;&#x6807;&#x53EF;&#x4EE5;&#x7ACB;&#x5373;&#x68C0;&#x67E5;&#x3001;&#x4FEE;&#x6539;&#x8D26;&#x53F7;&#x5BC6;&#x7801;&#x3001;&#x5F00;&#x5173;&#x5F00;&#x673A;&#x81EA;&#x542F;&#x3001;&#x6253;&#x5F00;&#x65E5;&#x5FD7;&#x6216;&#x9000;&#x51FA;&#x3002;
5. &#x5BC6;&#x7801;&#x4EC5;&#x4FDD;&#x5B58;&#x5728;&#x672C;&#x673A;&#xFF0C;&#x4F7F;&#x7528; Windows DPAPI &#x4E3A;&#x5F53;&#x524D;&#x7528;&#x6237;&#x52A0;&#x5BC6;&#x3002;

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
