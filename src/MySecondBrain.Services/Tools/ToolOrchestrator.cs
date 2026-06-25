using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

/// <summary>
/// Orchestrates the full 14-tool surface with parallel execution support.
/// Independent tools execute concurrently (Task.WhenAll, max 10),
/// while dependent tools are grouped sequentially. Each tool call
/// is wrapped in a try/catch to isolate failures.
/// </summary>
public class ToolOrchestrator : IToolOrchestrator
{
    private readonly IEnumerable<IToolExecutor> _executors;
    private readonly ILogger<ToolOrchestrator> _logger;

    private const int MaxConcurrentTools = 10;

    /// <summary>
    /// Tools that read file system state without modifying it.
    /// Auto-approved within workspace/artifacts/wiki directories.
    /// Feature 17: used by AreIndependent and auto-approval gating.
    /// </summary>
    private static readonly HashSet<string> ReadFileOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "read_file", "list_files", "search_files"
    };

    /// <summary>
    /// Tools that modify file system state. Always require user confirmation.
    /// Feature 17: used by AreIndependent and auto-approval gating.
    /// </summary>
    private static readonly HashSet<string> WriteFileOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "apply_diff", "write_to_file"
    };

    public ToolOrchestrator(
        IEnumerable<IToolExecutor> executors,
        ILogger<ToolOrchestrator> logger)
    {
        _executors = executors;
        _logger = logger;
    }

    /// <summary>
    /// Processes tool calls with parallel execution scaffolding.
    /// Independent tools execute concurrently via Task.WhenAll (max 10).
    /// Each tool is wrapped in try/catch to isolate failures.
    /// </summary>
    public async Task<IReadOnlyList<ToolResult>> ProcessToolCallsAsync(
        IReadOnlyList<ToolCall> toolCalls,
        ToolAutoApprovalSettings settings,
        CancellationToken ct)
    {
        if (toolCalls.Count == 0)
            return Array.Empty<ToolResult>();

        _logger.LogDebug("Processing {Count} tool calls (max {Max} concurrent)",
            toolCalls.Count, MaxConcurrentTools);

        // Group independent tools for parallel execution
        var groups = GroupIndependentTools(toolCalls);
        var results = new List<ToolResult>(toolCalls.Count);

        foreach (var group in groups)
        {
            // Execute group members in parallel
            var tasks = group.Select(tc => ExecuteSingleToolSafe(tc, settings, ct));
            var groupResults = await Task.WhenAll(tasks);
            results.AddRange(groupResults);
        }

        return results.AsReadOnly();
    }

    /// <summary>
    /// Executes a single tool call with try/catch wrapping.
    /// Validates the tool call before execution.
    /// </summary>
    private async Task<ToolResult> ExecuteSingleToolSafe(
        ToolCall toolCall,
        ToolAutoApprovalSettings settings,
        CancellationToken ct)
    {
        try
        {
            var executor = _executors.FirstOrDefault(e => e.ToolName == toolCall.Name);
            if (executor == null)
                return new ToolResult(false, "", $"Unknown tool: {toolCall.Name}");

            var validation = await executor.ValidateAsync(toolCall, ct);
            if (!validation.IsValid)
                return new ToolResult(false, "", validation.ErrorMessage ?? "Validation failed");

            return await executor.ExecuteAsync(toolCall, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed for {ToolName}", toolCall.Name);
            return new ToolResult(false, "", $"Tool execution error: {ex.Message}");
        }
    }

    /// <summary>
    /// Groups tool calls into batches of independent tools for parallel execution.
    /// Each batch contains up to MaxConcurrentTools tools executed together.
    /// Stub: all tools are treated as independent.
    /// Feature 17 will implement dependency detection via AreIndependent.
    /// </summary>
    private static List<List<ToolCall>> GroupIndependentTools(IReadOnlyList<ToolCall> toolCalls)
    {
        // Stub: all tools are independent, split into batches of MaxConcurrentTools
        var groups = new List<List<ToolCall>>();
        for (int i = 0; i < toolCalls.Count; i += MaxConcurrentTools)
        {
            var batch = toolCalls.Skip(i).Take(MaxConcurrentTools).ToList();
            groups.Add(batch);
        }
        return groups;
    }

    /// <summary>
    /// Determines whether two tool calls can execute in parallel.
    /// Stub: returns true for all tool combinations.
    /// Feature 17 will implement real dependency detection
    /// (e.g., write_to_file depends on read_file for the same path).
    /// </summary>
    public static bool AreIndependent(ToolCall a, ToolCall b)
    {
        // Stub: all tools are independent
        // Feature 17: check for conflicting file paths, resource locks, etc.
        _ = a;
        _ = b;
        return true;
    }

    public IReadOnlyList<ToolDefinition> GetAvailableToolDefinitions()
    {
        var definitions = new List<ToolDefinition>();
        foreach (var executor in _executors)
        {
            definitions.Add(new ToolDefinition(
                executor.ToolName,
                executor.Description,
                executor.ParametersJsonSchema));
        }
        return definitions.AsReadOnly();
    }

    /// <summary>
    /// Checks whether a tool is enabled. Stub: all 14 tools enabled by default.
    /// Feature 17 will implement per-chat and global tool toggles.
    /// </summary>
    public bool IsToolEnabled(string toolName)
    {
        return _executors.Any(e => e.ToolName == toolName);
    }

    /// <summary>
    /// Returns auto-approval settings with the 5 file operation tools
    /// properly categorized. Read operations (read_file, list_files,
    /// search_files) are auto-approved within known-safe directories.
    /// Write operations (apply_diff, write_to_file) require confirmation.
    /// </summary>
    public ToolAutoApprovalSettings GetAutoApprovalSettings()
    {
        var settings = new ToolAutoApprovalSettings();

        // File operation tools: reads auto-approved, writes restricted
        settings.AutoApproveReadFile = true;
        settings.AutoApproveListFiles = true;
        settings.AutoApproveSearchFiles = true;
        settings.AutoApproveApplyDiff = false;
        settings.AutoApproveWriteToFile = false;

        // Knowledge and communication tools: restricted by default
        settings.AutoApproveWebSearch = false;
        settings.AutoApproveWebFetch = false;
        settings.AutoApproveWikiSearch = false;
        settings.AutoApproveMemory = false;
        settings.AutoApproveSkillLoad = false;
        settings.AutoApproveAskUserInput = false;
        settings.AutoApprovePresentFiles = false;
        settings.AutoApproveImageSearch = false;

        return settings;
    }
}
