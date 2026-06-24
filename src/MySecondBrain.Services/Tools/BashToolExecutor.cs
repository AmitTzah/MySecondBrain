using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

/// <summary>
/// Executes bash commands in an isolated workspace directory.
/// On Windows, uses cmd.exe for cross-platform commands and tries Git Bash/WSL for .sh scripts.
/// </summary>
public class BashToolExecutor : IToolExecutor
{
    private readonly ILogger<BashToolExecutor> _logger;
    private readonly string? _wikiDirectoryPath;

    /// <summary>
    /// Workspace directory for all bash tool executions.
    /// Path: %LOCALAPPDATA%\MySecondBrain\workspace\
    /// </summary>
    public static readonly string WorkspacePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MySecondBrain",
        "workspace");

    private static bool _cleanupPerformed;
    private static readonly object CleanupLock = new();

    private const int CleanupAgeHours = 24;

    // Cached bash availability
    private static readonly Lazy<bool> BashAvailableLazy = new(ProbeBashAvailable, LazyThreadSafetyMode.ExecutionAndPublication);

    private const string BashSchema = """
        {
            "type": "object",
            "properties": {
                "command": {
                    "type": "string",
                    "description": "The command to execute. On Windows, cross-platform tools (python, pip, npm) work natively in cmd.exe. For .sh scripts, Git Bash or WSL is required."
                }
            },
            "required": ["command"]
        }
        """;

    public BashToolExecutor(
        ILogger<BashToolExecutor> logger,
        string? wikiDirectoryPath = null)
    {
        _logger = logger;
        _wikiDirectoryPath = wikiDirectoryPath;
    }

    public string ToolName => "bash";

    public string Description
    {
        get
        {
            var bashAvailable = BashAvailableLazy.Value;
            var desc = "Execute commands in the workspace directory. ";
            desc += "On Windows, uses cmd.exe for cross-platform commands (python, pip, npm, pandoc). ";
            if (bashAvailable)
                desc += "Git Bash or WSL is available for .sh scripts. ";
            else
                desc += ".sh scripts require Git Bash or WSL (neither detected). ";
            desc += "For multi-line file writing, prefer the text_editor tool. ";
            desc += $"Workspace: {WorkspacePath}";
            return desc;
        }
    }

    public string ParametersJsonSchema => BashSchema;

    public bool RequiresUserConfirmation => true;
    public ToolRiskLevel RiskLevel => ToolRiskLevel.Medium;
    public bool CanAutoApprove => false;

    /// <summary>
    /// Probe whether bash (Git Bash or WSL) is available on this system.
    /// </summary>
    public static bool DetectBashAvailable() => BashAvailableLazy.Value;

    /// <summary>
    /// Run periodic cleanup of workspace files older than 24 hours.
    /// Called once per app lifetime on first tool execution.
    /// Creates the workspace directory if it does not exist.
    /// </summary>
    public static void PerformStartupCleanup()
    {
        if (_cleanupPerformed)
            return;

        lock (CleanupLock)
        {
            if (_cleanupPerformed)
                return;

            try
            {
                // Ensure workspace directory exists
                if (!Directory.Exists(WorkspacePath))
                {
                    Directory.CreateDirectory(WorkspacePath);
                    _cleanupPerformed = true;
                    return;
                }

                var cutoff = DateTime.UtcNow.AddHours(-CleanupAgeHours);
                int deletedCount = 0;

                foreach (var file in Directory.GetFiles(WorkspacePath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var lastWrite = File.GetLastWriteTimeUtc(file);
                        if (lastWrite < cutoff)
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                    }
                    catch
                    {
                        // Best-effort cleanup — skip files that can't be deleted
                    }
                }

                // Remove empty directories
                foreach (var dir in Directory.GetDirectories(WorkspacePath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length)) // Deepest first
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            Directory.Delete(dir);
                    }
                    catch
                    {
                        // Best-effort
                    }
                }

                // Serilog already configured — this logs at debug level
                Debug.WriteLine($"[BashToolExecutor] Startup cleanup: {deletedCount} old files removed from {WorkspacePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BashToolExecutor] Startup cleanup failed: {ex.Message}");
            }
            finally
            {
                _cleanupPerformed = true;
            }
        }
    }

    public Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct)
    {
        var command = ParseCommand(toolCall);
        if (command is null)
        {
            return Task.FromResult(new ToolValidationResult(
                false, "Could not parse 'command' from tool call arguments.", ToolRiskLevel.Medium));
        }

        // Check for heredoc patterns — redirect to text_editor
        if (ContainsHeredoc(command))
        {
            return Task.FromResult(new ToolValidationResult(
                false,
                "Heredoc syntax detected. For multi-line file writing, use the text_editor tool instead of bash heredocs.",
                ToolRiskLevel.Medium));
        }

        // Check for absolute paths outside workspace
        if (ContainsBlockedPath(command))
        {
            return Task.FromResult(new ToolValidationResult(
                false,
                "Command contains absolute path references outside the workspace (C:\\, %, or ~ patterns). " +
                "File operations outside the workspace require user confirmation via the ask_user_input tool.",
                ToolRiskLevel.Medium));
        }

        // Check for wiki directory writes
        if (_wikiDirectoryPath is not null && WritesToWiki(command, _wikiDirectoryPath))
        {
            return Task.FromResult(new ToolValidationResult(
                false,
                "Writing to the wiki directory via bash is not allowed. Use the text_editor tool and the Write-to-Wiki pipeline instead.",
                ToolRiskLevel.Medium));
        }

        return Task.FromResult(new ToolValidationResult(true, null, ToolRiskLevel.Medium));
    }

    public async Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct)
    {
        var command = ParseCommand(toolCall);
        if (command is null)
        {
            return new ToolResult(false, string.Empty, "Could not parse 'command' from tool call arguments.");
        }

        // Run startup cleanup once per app lifetime
        PerformStartupCleanup();

        // Ensure workspace directory exists
        EnsureWorkspaceDirectory();

        // Determine shell and arguments based on command type
        var (fileName, arguments) = ResolveShell(command);

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = WorkspacePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                }
            };

            process.Start();

            // Read stdout and stderr concurrently
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            // Wait for process to exit with cancellation support
            using var registration = ct.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); }
                catch { /* best-effort kill on cancellation */ }
            });

            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            var exitCode = process.ExitCode;

            if (exitCode == 0)
            {
                var content = stdout.Length > 0 ? stdout
                    : stderr.Length > 0 ? stderr
                    : "Command completed successfully (no output).";
                return new ToolResult(true, content, null);
            }
            else
            {
                var errorMsg = $"Command exited with code {exitCode}.";
                var content = stdout.Length > 0
                    ? $"{errorMsg}\n{stdout}\n{stderr}"
                    : $"{errorMsg}\n{stderr}";
                return new ToolResult(false, content, errorMsg);
            }
        }
        catch (OperationCanceledException)
        {
            return new ToolResult(false, string.Empty, "Command execution was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute bash command");
            return new ToolResult(false, string.Empty, $"Failed to execute command: {ex.Message}");
        }
    }

    public string GetConfirmationDescription(ToolCall toolCall)
    {
        var command = ParseCommand(toolCall);
        if (command is null)
            return "Execute a bash command";
        return $"Execute: {TruncateForDisplay(command)}";
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Parses the 'command' field from the tool call's JSON arguments.
    /// </summary>
    private static string? ParseCommand(ToolCall toolCall)
    {
        try
        {
            using var doc = JsonDocument.Parse(toolCall.Arguments);
            if (doc.RootElement.TryGetProperty("command", out var cmdProp))
                return cmdProp.GetString();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves the shell executable and arguments based on the command type.
    /// .sh scripts are routed to Git Bash or WSL; everything else uses cmd.exe.
    /// </summary>
    private static (string fileName, string arguments) ResolveShell(string command)
    {
        // .sh file extension at word boundary — try Git Bash, then WSL
        if (System.Text.RegularExpressions.Regex.IsMatch(
                command, @"\b[\w./\\-]*\.sh(\s|$|;|&|\|)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var gitBashPath = @"C:\Program Files\Git\bin\bash.exe";
            if (File.Exists(gitBashPath))
            {
                return (gitBashPath, BashEscapeArg(command, isBash: true));
            }

            // Try WSL
            return ("wsl", $"bash -c \"{BashEscapeArg(command, isBash: true)}\"");
        }

        // Default: cmd.exe for cross-platform commands (python, pip, npm, etc.)
        return ("cmd.exe", $"/c \"{EscapeArg(command)}\"");
    }

    /// <summary>
    /// Escapes a command string for cmd.exe. Only escapes double quotes.
    /// </summary>
    private static string EscapeArg(string command)
    {
        return command.Replace("\"", "\\\"");
    }

    /// <summary>
    /// Escapes a command string for bash (Git Bash or WSL).
    /// Escapes: double quotes, dollar signs, backticks, backslashes, exclamation marks.
    /// </summary>
    private static string BashEscapeArg(string command, bool isBash)
    {
        if (!isBash)
            return EscapeArg(command);

        // Bash requires escaping of: " $ ` \ !
        var result = command
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("$", "\\$")
            .Replace("`", "\\`")
            .Replace("!", "\\!");
        return result;
    }

    /// <summary>
    /// Detects heredoc patterns (cat greater-than file less-than-less-than 'EOF') in the command.
    /// </summary>
    private static bool ContainsHeredoc(string command)
    {
        // Common heredoc patterns
        return command.Contains("<<", StringComparison.Ordinal) &&
               (command.Contains("cat ", StringComparison.OrdinalIgnoreCase) ||
                command.Contains("tee ", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Scans the command for absolute path patterns that reference locations outside the workspace.
    /// Blocks: drive letters (C:\), environment variables (%), home directory (~).
    /// All absolute paths (X:\) are blocked — the model should use relative paths for workspace files
    /// and the text_editor tool for files outside the workspace.
    /// </summary>
    public static bool ContainsBlockedPath(string command)
    {
        if (string.IsNullOrEmpty(command))
            return false;

        // Check for drive letter patterns: X:\ or X:/
        // All absolute drive letter paths are blocked — workspace isolation requires relative paths
        if (System.Text.RegularExpressions.Regex.IsMatch(command, @"[A-Za-z]:[\\/]"))
            return true;

        // Check for environment variable references: %VAR%
        if (command.Contains('%', StringComparison.Ordinal))
        {
            // Scan for %WORD% patterns (environment variables)
            var envVarMatches = System.Text.RegularExpressions.Regex.Matches(command, @"%[A-Za-z_][A-Za-z0-9_]*%");
            if (envVarMatches.Count > 0)
            {
                // Allow LOCALAPPDATA and USERPROFILE (they resolve to the workspace path chain)
                foreach (System.Text.RegularExpressions.Match m in envVarMatches)
                {
                    var varName = m.Value.Trim('%');
                    if (!string.Equals(varName, "LOCALAPPDATA", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(varName, "USERPROFILE", StringComparison.OrdinalIgnoreCase))
                    {
                        return true; // Block unknown environment variables
                    }
                }
            }
        }

        // Check for ~ path references (user home directory shortcut)
        // Must be a standalone ~ or ~\ or ~/ (not part of a path like ~~ or a command flag)
        if (System.Text.RegularExpressions.Regex.IsMatch(command, @"(^|\s)~[\\/]"))
            return true;

        return false;
    }

    /// <summary>
    /// Detects if the command attempts to write to the wiki directory.
    /// Checks for redirection (>, >>) to paths under the wiki directory.
    /// </summary>
    private static bool WritesToWiki(string command, string wikiDirectoryPath)
    {
        if (string.IsNullOrEmpty(wikiDirectoryPath) || string.IsNullOrEmpty(command))
            return false;

        // Normalize wiki path for comparison
        var normalizedWiki = wikiDirectoryPath.TrimEnd('\\', '/');

        // Check for output redirection to a path under the wiki directory
        // Patterns: > path, >> path, 1> path, 2> path
        var redirectMatches = System.Text.RegularExpressions.Regex.Matches(
            command,
            @"[\d]*>{1,2}\s*(\S+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in redirectMatches)
        {
            if (match.Groups.Count < 2)
                continue;

            var targetPath = match.Groups[1].Value.Trim('"', '\'');
            if (string.IsNullOrEmpty(targetPath))
                continue;

            // Resolve relative paths against workspace
            string fullPath;
            if (Path.IsPathRooted(targetPath))
            {
                fullPath = targetPath;
            }
            else
            {
                fullPath = Path.GetFullPath(Path.Combine(WorkspacePath, targetPath));
            }

            // Check if the resolved path is under the wiki directory
            if (fullPath.StartsWith(normalizedWiki, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Ensures the workspace directory exists, creating it if necessary.
    /// </summary>
    private void EnsureWorkspaceDirectory()
    {
        try
        {
            if (!Directory.Exists(WorkspacePath))
            {
                Directory.CreateDirectory(WorkspacePath);
                _logger.LogInformation("Created workspace directory at {WorkspacePath}", WorkspacePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create workspace directory at {WorkspacePath}", WorkspacePath);
            throw;
        }
    }

    /// <summary>
    /// Truncates a command string for display in confirmation dialogs.
    /// </summary>
    private static string TruncateForDisplay(string command, int maxLength = 200)
    {
        if (command.Length <= maxLength)
            return command;
        return command[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Probes for bash availability: checks for Git Bash at the standard path,
    /// then tries WSL via `wsl --status`. Result is cached via Lazy{T}.
    /// </summary>
    private static bool ProbeBashAvailable()
    {
        try
        {
            var gitBashPath = @"C:\Program Files\Git\bin\bash.exe";
            if (File.Exists(gitBashPath))
                return true;

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wsl",
                    Arguments = "--status",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();

            if (!process.WaitForExit(3000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
