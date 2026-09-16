# SZU Campus Network Monitor

Windows 托盘程序：每分钟检查一次网络；首次 Ping 失败时自动发现当前 SRun 门户并重新认证。

## 中文说明

1. 从 [Releases](../../releases) 下载并安装 `SZUNetworkMonitor-Setup.exe`。
2. 首次运行时输入校园网账号和密码。
3. 程序驻留在系统托盘，每 60 秒检查一次网络；每轮最多 Ping 5 次，第一包失败即进入一次重连流程。
4. 右键托盘图标可立即检查、修改账号密码、设置开机自启、打开日志或退出。

密码仅保存在本机，并使用当前 Windows 用户的 DPAPI 加密。密码不会出现在命令行、日志或网络状态提示中。

## 动态门户发现

程序不再假定某个教学区认证 IP 或 `ac_id`。检测到断网后，登录助手会：

1. 对固定公共 IP 的 HTTP 地址发送不跟随重定向的请求，以触发 captive portal。
2. 读取 30x 响应的 `Location`，解析门户主机、`ac_id`、`ac-ip` 或 `ac_ip`、SSID 和客户端 IP。
3. 用本次发现到的 `ac_id` 生成 challenge、用户信息、校验和及登录请求。
4. 优先把 TCP 连接直接发往发现到的 `ac_ip`；请求 URL、`Host`、TLS SNI 与证书校验仍保持为门户主机（例如 `net.szu.edu.cn`）。不会关闭 TLS 校验，也不会静默改连旧教学区地址。

因此教学区、`SZU_WLAN` 等环境会跟随真实门户参数工作。日志只保留门户发现与认证阶段；MAC、账号、密码和 IP 地址会被隐藏。

如果 HTTP 探测因特殊网络环境无法得到 30x 跳转，登录助手只支持显式的最后兜底：必须同时传入 `--fallback-host`、`--fallback-ac-id`、`--fallback-ac-ip` 和 `--fallback-client-ip`。这些参数没有内置默认值，GUI 默认不会启用它们。

## 托盘状态

| 状态 | 含义 |
| --- | --- |
| `正在检测 1/5` | 正在执行本轮网络检查。 |
| `正在重连` | 首个 Ping 失败，准备认证。 |
| `正在发现门户` | 正在读取 captive portal 的重定向参数。 |
| `重连成功` | SRun 认证已完成。 |
| `重连失败：…` | 显示已脱敏的可读原因；可从托盘菜单打开日志进一步查看。 |
| `认证冷却中：…` | 上一次认证失败后，60 秒冷却期尚未结束。 |

同一 Windows 用户只能运行一个实例。日志中的每一行带有 PID，便于确认是否存在重复进程。程序使用一个可取消的计时器和一个串行认证闸门：认证失败后至少 60 秒才会再次尝试，退出时会停止计时器并取消正在进行的认证助手。

## 安全与隐私

- 密码保存在 `%LOCALAPPDATA%\SZUNetworkMonitor\settings.json`，并由 Windows DPAPI 绑定到当前用户。
- 密码通过标准输入传给本地登录助手，不会放进命令行。
- 认证使用正常的 HTTPS 证书校验；不会修改 `hosts` 文件，也不需要管理员权限。
- 日志和失败提示不记录 MAC、账号、密码或完整 IP 地址。

## 故障排查

若显示“未发现当前网络的认证门户”，请确认已接入 SZU 校园网且浏览器能看到认证跳转页面。若显示“无法连接认证门户”或“认证门户连接超时”，请从托盘菜单打开日志，并在反馈前确认其中没有其他个人信息。

## Build and test

Requirements: Windows 10 or later, .NET 8 SDK, Go 1.22 or later, and optionally Inno Setup 6 for the installer.

```powershell
cd src\SzuLoginHelper
go test ./...

dotnet build ..\SZUNetworkMonitor\SZUNetworkMonitor.csproj
dotnet test ..\SZUNetworkMonitor.Tests\SZUNetworkMonitor.Tests.csproj

cd ..\..
.\build.ps1
```

The build creates `artifacts\publish\` and, when Inno Setup is available, `artifacts\SZUNetworkMonitor-Setup.exe`.

## Release process

GitHub Actions builds and publishes the installer and portable zip for a version tag.

```powershell
git tag v1.0.2
git push origin main --tags
```

## License and attribution

The local SRun helper is a modified derivative of [nnothing1/szu-srun-login](https://github.com/nnothing1/szu-srun-login), licensed under AGPL-3.0. This project is distributed under AGPL-3.0-or-later. See [LICENSE](LICENSE) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
