using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SZUNetworkMonitor;

internal sealed record LoginResult(bool Success, string Message, string? Error);

internal static class LoginClient
{
    private static readonly Regex IPv4Address = new(@"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])", RegexOptions.CultureInvariant);
    private static readonly Regex MacAddress = new(@"(?i)(?<![0-9a-f])(?:[0-9a-f]{2}[:-]){5}[0-9a-f]{2}(?![0-9a-f])", RegexOptions.CultureInvariant);

    public static async Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        var helperPath = Path.Combine(AppContext.BaseDirectory, "szu-login-helper.exe");
        if (!File.Exists(helperPath))
        {
            return new LoginResult(false, "Login helper is missing. Reinstall the application.", "szu-login-helper.exe was not found.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = helperPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--username");
        startInfo.ArgumentList.Add(username);
        startInfo.ArgumentList.Add("--password-stdin");
        startInfo.ArgumentList.Add("--json");

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return new LoginResult(false, "Unable to start the login helper.", "Process.Start returned false.");
        }

        await process.StandardInput.WriteAsync(password.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            return new LoginResult(false, "Login timed out.", "The login helper did not finish within the allowed time.");
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        try
        {
            var result = JsonSerializer.Deserialize<LoginResult>(standardOutput, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result is not null)
            {
                return result with
                {
                    Message = SanitizeDiagnostic(result.Message, username),
                    Error = result.Error is null ? null : SanitizeDiagnostic(result.Error, username)
                };
            }
        }
        catch (JsonException)
        {
            // Include no credentials in the fallback message or log.
        }

        var helperError = string.IsNullOrWhiteSpace(standardError) ? "No diagnostic output." : SanitizeDiagnostic(standardError.Trim(), username);
        return new LoginResult(false, "Login helper returned an invalid response.", $"Exit code {process.ExitCode}: {helperError}");
    }

    internal static string ToUserFacingFailure(LoginResult result)
    {
        var diagnostic = result.Error ?? result.Message;
        if (diagnostic.Contains("portal discovery failed", StringComparison.OrdinalIgnoreCase))
        {
            return "未发现当前网络的认证门户";
        }
        if (diagnostic.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        {
            return "认证门户连接超时";
        }
        if (diagnostic.Contains("direct request to the discovered portal failed", StringComparison.OrdinalIgnoreCase))
        {
            return "无法连接认证门户";
        }
        if (diagnostic.Contains("portal rejected", StringComparison.OrdinalIgnoreCase))
        {
            return "认证门户拒绝登录请求";
        }
        return "认证服务返回异常";
    }

    private static string SanitizeDiagnostic(string diagnostic, string username)
    {
        var withoutUsername = string.IsNullOrWhiteSpace(username)
            ? diagnostic
            : diagnostic.Replace(username, "[账号已隐藏]", StringComparison.OrdinalIgnoreCase);
        var withoutMac = MacAddress.Replace(withoutUsername, "[MAC 已隐藏]");
        return IPv4Address.Replace(withoutMac, "[IP 已隐藏]");
    }
}
