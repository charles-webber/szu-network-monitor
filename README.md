# SZU Campus Network Monitor

A Windows tray application for automatically reconnecting Shenzhen University SRun campus networking.

## For normal users

Do not clone this repository or run PowerShell scripts. Open the project's GitHub **Releases** page and download `SZUNetworkMonitor-Setup.exe`.

1. Run the installer.
2. Open **SZU Campus Network Monitor**.
3. Enter the campus-network username and password once.
4. Leave `Start with Windows` enabled, then choose Save.

The application starts in the system tray. It checks `www.baidu.com` once per minute, sends up to five ping packets per check, and reconnects immediately after the first lost reply. The tray menu offers an immediate check, settings, startup control, logs, and exit.

The login helper routes `net.szu.edu.cn` directly to the configured campus authentication IP while retaining HTTPS hostname verification. This means first-time setup does not need administrator privileges and never changes the Windows `hosts` file.

## For maintainers

Prerequisites:

- .NET 8 SDK
- Go 1.22 or later
- Inno Setup 6 (only needed for `Setup.exe`)

Build a self-contained x64 portable app and, when Inno Setup is available, an installer:

```powershell
.\build.ps1
```

The project builds the Go login helper first, copies it beside the WinForms tray app, and writes deliverables to `artifacts/`. A version tag such as `v1.0.0` triggers the same process in GitHub Actions and publishes both the installer and portable zip to GitHub Releases.

## Security

The app stores the password under `%LOCALAPPDATA%\SZUNetworkMonitor\settings.json`, encrypted with Windows DPAPI for the current user. It passes the decrypted password to the local helper through standard input, never on a command line, and does not write it to logs.

## Compatibility

This implementation targets the SZU teaching-area SRun endpoint (`net.szu.edu.cn`). It is not a dormitory ePortal client. Campus network endpoints can change; please open an issue with sanitized logs if login stops working.

## License and attribution

The login helper is a modified derivative of [`nnothing1/szu-srun-login`](https://github.com/nnothing1/szu-srun-login), licensed under AGPL-3.0. This repository is therefore distributed under AGPL-3.0-or-later. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
