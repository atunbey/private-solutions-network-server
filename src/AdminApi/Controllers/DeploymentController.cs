using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers;

[ApiController]
[Route("api/admin/deploy")]
[Authorize(Roles = "psn-admin")]
public class DeploymentController(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<DeploymentController> logger) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var repoPath = configuration["Deploy:RepoPath"] ?? "/home/atun/private-solutions-network-server";
        var branch = configuration["Deploy:GitBranch"] ?? "main";
        var gitHubRepo = configuration["Deploy:GitHubRepo"] ?? "atunbey/private-solutions-network-server";
        var currentRelease = Environment.GetEnvironmentVariable("PSN_RELEASE") ?? configuration["PSN_RELEASE"] ?? string.Empty;

        var currentCommit = await TryRunCommandAsync($"git -C {EscapeShellArg(repoPath)} rev-parse --short HEAD", 10, cancellationToken);
        var latestCommit = await TryGetLatestCommitShortShaAsync(gitHubRepo, branch, cancellationToken);
        var canTriggerUpdate = string.Equals(configuration["Deploy:EnableCommandExecution"], "true", StringComparison.OrdinalIgnoreCase)
                               && !string.IsNullOrWhiteSpace(configuration["Deploy:UpdateCommand"]);

        return Ok(new
        {
            currentRelease,
            currentCommit = currentCommit?.StdOut,
            latestCommit,
            updateAvailable = !string.IsNullOrWhiteSpace(currentCommit?.StdOut)
                              && !string.IsNullOrWhiteSpace(latestCommit)
                              && !string.Equals(currentCommit!.StdOut, latestCommit, StringComparison.OrdinalIgnoreCase),
            canTriggerUpdate,
            branch,
            repoPath
        });
    }

    [HttpPost("update")]
    public async Task<IActionResult> TriggerUpdate(CancellationToken cancellationToken)
    {
        var enabled = string.Equals(configuration["Deploy:EnableCommandExecution"], "true", StringComparison.OrdinalIgnoreCase);
        var command = configuration["Deploy:UpdateCommand"];

        if (!enabled || string.IsNullOrWhiteSpace(command))
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new
            {
                message = "Update command execution is not enabled. Set Deploy:EnableCommandExecution=true and Deploy:UpdateCommand."
            });
        }

        logger.LogInformation("Deployment update command started.");
        var result = await RunCommandAsync(command, 1800, cancellationToken);
        logger.LogInformation("Deployment update command finished with exit code {ExitCode}.", result.ExitCode);

        return Ok(new
        {
            exitCode = result.ExitCode,
            success = result.ExitCode == 0,
            stdOut = Truncate(result.StdOut, 16000),
            stdErr = Truncate(result.StdErr, 16000)
        });
    }

    private async Task<string?> TryGetLatestCommitShortShaAsync(string repo, string branch, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("psn-admin-api/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var url = $"https://api.github.com/repos/{repo}/commits/{branch}";
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("sha", out var shaNode))
            {
                return null;
            }

            var sha = shaNode.GetString();
            return string.IsNullOrWhiteSpace(sha) ? null : sha[..Math.Min(7, sha.Length)];
        }
        catch
        {
            return null;
        }
    }

    private async Task<CommandResult?> TryRunCommandAsync(string command, int timeoutSeconds, CancellationToken cancellationToken)
    {
        try
        {
            return await RunCommandAsync(command, timeoutSeconds, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<CommandResult> RunCommandAsync(string command, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var isWindows = OperatingSystem.IsWindows();
        var fileName = isWindows ? "powershell" : "/bin/bash";
        var args = isWindows
            ? $"-NoProfile -NonInteractive -Command \"{command.Replace("\"", "\\\"")}\""
            : $"-lc \"{command.Replace("\"", "\\\"")}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var waitTask = process.WaitForExitAsync(cancellationToken);

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
        var completed = await Task.WhenAny(waitTask, timeoutTask);

        if (completed == timeoutTask)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignored
            }

            throw new TimeoutException($"Command timed out after {timeoutSeconds} seconds.");
        }

        await waitTask;
        var stdOut = await stdoutTask;
        var stdErr = await stderrTask;

        return new CommandResult(process.ExitCode, stdOut, stdErr);
    }

    private static string EscapeShellArg(string value)
    {
        return $"'{value.Replace("'", "'\\''")}'";
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
        {
            return text;
        }

        return text[..maxLen] + "\n...truncated...";
    }

    private sealed record CommandResult(int ExitCode, string StdOut, string StdErr);
}
