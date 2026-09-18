# 深圳大学校园网自动认证 · SZU Campus Network Monitor

[![Release](https://img.shields.io/github/v/release/charles-webber/szu-network-monitor?display_name=tag&sort=semver)](https://github.com/charles-webber/szu-network-monitor/releases)
[![Build](https://github.com/charles-webber/szu-network-monitor/actions/workflows/release.yml/badge.svg)](https://github.com/charles-webber/szu-network-monitor/actions/workflows/release.yml)
[![License: AGPL-3.0-or-later](https://img.shields.io/badge/License-AGPL--3.0--or--later-blue.svg)](LICENSE)

面向 **深圳大学（SZU）校园网 / SRun** 的 Windows 自动认证与断网重连工具。它常驻系统托盘，在 `SZU_WLAN`、教学区等不同网络环境中动态发现认证门户；当账号被其他设备顶下线、认证失效或网络出现假连通时，自动安全地重新认证。

**English:** A Windows tray app for Shenzhen University (SZU) campus-network SRun auto-login and reconnection. It dynamically discovers captive-portal parameters and supports SZU_WLAN and teaching-area networks without hard-coded AC IPs or `ac_id` values.

## 为什么选择它

- **为真实断网而做：** 账号被顶下线时自动处理，而不是只在 Wi-Fi 断开时工作。
- **兼容不同校区网络：** 动态读取当前门户的 `ac_id`、`ac-ip/ac_ip`、SSID 与客户端地址，不假设教学区地址。
- **能绕开代理/TUN Fake-IP：** 探测被代理拦截时，使用物理 WLAN/以太网网卡的 DNS 与源地址直连门户。
- **不牺牲安全：** 保留 `net.szu.edu.cn` 的 Host、TLS SNI 和证书验证；账号密码不会出现在命令行或日志中。
- **安静且克制：** 每 60 秒检测一次；单实例、认证串行和失败冷却避免反复登录。

## 快速开始

1. 从 [Releases](../../releases) 下载 `SZUNetworkMonitor-Setup.exe`，或使用 portable ZIP。
2. 安装后首次启动，输入校园网账号和密码。
3. 程序会留在 Windows 系统托盘；右键图标可立即检查、修改账号、打开日志或退出。

遇到连接问题，请先看 [常见问题与排障指南](docs/TROUBLESHOOTING.md)。提交 Issue 前，请移除账号、密码、MAC 和完整 IP 地址。

Windows 托盘程序：每分钟检查一次网络；连续两次 Ping 失败时自动发现当前 SRun 门户并重新认证。

## 中文说明

1. 从 [Releases](../../releases) 下载并安装 `SZUNetworkMonitor-Setup.exe`。
2. 首次运行时输入校园网账号和密码。
3. 程序驻留在系统托盘，每 60 秒检查一次网络；每轮最多 Ping 5 次，连续两次失败才进入一次重连流程。单次偶发丢包会继续检测，不会立即认证。
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

部分 Windows 代理或 TUN 网络会把认证服务器解析为 `198.18.0.0/15` 的 Fake-IP，或对白名单检测页返回普通响应。这种情况下，程序会改用活动的无线/有线物理网卡：从该网卡读取 IPv4 和 DNS，绑定该源地址直连解析 `net.szu.edu.cn`，并保持 `Host`、TLS SNI 与证书校验。它会从 SRun 门户页面动态读取 `ac_id` 和可用的 `ac-ip`，并由 challenge 返回当前客户端 IP；没有任何固定的教学区 IP 或 `ac_id` 回退。

## 托盘状态

| 状态 | 含义 |
| --- | --- |
| `正在检测 1/5` | 正在执行本轮网络检查。 |
| `正在重连` | 连续两次 Ping 失败，准备认证。 |
| `正在发现门户` | 正在读取 captive portal 的重定向参数。 |
| `重连成功` | SRun 认证已完成。 |
| `重连失败：…` | 显示已脱敏的可读原因；可从托盘菜单打开日志进一步查看。 |
| `认证冷却中：…` | 上一次认证失败后，60 秒冷却期尚未结束。 |

同一 Windows 用户只能运行一个实例。日志中的每一行带有 PID，便于确认是否存在重复进程。程序使用一个可取消的计时器、检查节流和串行认证闸门：每次检查结束后至少 60 秒才会开始下一轮；认证失败后至少 60 秒才会再次尝试，退出时会停止计时器并取消正在进行的认证助手。

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

测试会在本机启动一个临时 HTTP 服务，模拟 captive portal 返回的 302 `Location`；它不会断开当前网络，也不会向真实校园网提交认证。该测试会验证 `ac-ip` / `ac_ip`、不同 `ac_id`，以及这些动态参数在 challenge、用户信息、校验和和登录请求中的完整传递。

## Release process

GitHub Actions builds and publishes the installer and portable zip for a version tag.

```powershell
git tag v1.0.5
git push origin main --tags
```

## 致谢、贡献者与许可

本项目的每一段可靠性改进都建立在前人工作的基础上，感谢每一位贡献者：

- [charles-webber](https://github.com/charles-webber)：创建了初始的 Windows 托盘程序、安装包和用户文档，使项目能被普通使用者直接安装使用。
- [reraph-77](https://github.com/reraph-77)：持续改进动态门户发现、断网重连、单实例与认证可靠性。
- [nnothing1/szu-srun-login](https://github.com/nnothing1/szu-srun-login)：提供本项目使用并修改的 SRun 登录实现基础。

欢迎通过 [Issues](https://github.com/charles-webber/szu-network-monitor/issues) 报告不同 SSID、校区或网络软件组合下的兼容性情况，也欢迎提交文档、测试和代码改进。具体方式见 [贡献指南](CONTRIBUTING.md)。

This project is built on the work of its contributors and the upstream SRun implementation. Contributions, compatibility reports, documentation improvements, and tests are warmly welcome.

The local SRun helper is a modified derivative of [nnothing1/szu-srun-login](https://github.com/nnothing1/szu-srun-login), licensed under AGPL-3.0. This project is distributed under AGPL-3.0-or-later. See [LICENSE](LICENSE) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
