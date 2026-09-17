# 参与贡献

感谢你愿意改进深圳大学校园网自动认证工具。无论是报告某个 SSID 的兼容性、补充文档、完善测试，还是提交代码，都会帮助更多同学稳定联网。

## 提交 Issue 前

请说明：

- 使用的版本号和 Windows 版本；
- 网络名称（例如 `SZU_WLAN` 或教学区网络）；
- 预期行为、实际行为和可复现步骤；
- 托盘状态与经过脱敏的日志片段。

**请不要提交**校园网账号、密码、MAC 地址、完整私有 IP、门户 URL 中的 `uaddress` 或 `umac` 参数。程序的日志设计为不记录这些信息，但粘贴前仍请自行检查。

## 开发与测试

项目由 WinForms GUI 与 Go SRun helper 组成。提交前请在 Windows 上运行：

```powershell
cd src\SzuLoginHelper
go test ./...

dotnet build ..\SZUNetworkMonitor\SZUNetworkMonitor.csproj
dotnet test ..\SZUNetworkMonitor.Tests\SZUNetworkMonitor.Tests.csproj
```

涉及认证逻辑的改动，请新增或更新测试，至少覆盖参数解析、错误提示与不会泄露隐私的日志行为。不要添加固定的校区 IP、默认 `ac_id`、跳过 TLS 证书校验或明文凭据。

## 提交 Pull Request

1. 说明问题和解决思路。
2. 保持改动小而聚焦，并补充相应测试或文档。
3. 在描述中列出已执行的测试。
4. 如改动来自其他开源项目，请保留原有许可证和归属说明。

我们会在 Release Notes 与 README 中感谢有实质贡献的维护者和社区贡献者。
