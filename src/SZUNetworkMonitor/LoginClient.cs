using System.Diagnostics;
using System.Text.Json;

namespace SZUNetworkMonitor;

internal sealed record LoginResult(bool Success, string Message, string? Error);

internal static class LoginClient
{
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
                return result;
            }
        }
        catch (JsonException)
        {
            // Include no credentials in the fallback message or log.
        }

        var helperError = string.IsNullOrWhiteSpace(standardError) ? "No diagnostic output." : standardError.Trim();
        return new LoginResult(false, "Login helper returned an invalid response.", $"Exit code {process.ExitCode}: {helperError}");
    }
}
