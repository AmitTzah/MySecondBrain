using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

/// <summary>
/// Orchestrates tool execution across the full 14-tool surface.
/// Supports parallel execution of independent tools (via Task.WhenAll,
/// max 10 concurrent) while maintaining sequential execution for
/// dependent tool calls. Each tool is executed in a try/catch wrapper
/// to isolate failures.
/// </summary>
public interface IToolOrchestrator
{
    /// <summary>
    /// Processes a batch of tool calls. Independent tools execute in
    /// parallel (Task.WhenAll, max 10 concurrent). Dependent tools
    /// execute in sequential groups. Each tool is wrapped in try/catch
    /// so a single tool failure doesn't cancel the entire batch.
    /// </summary>
    Task<IReadOnlyList<ToolResult>> ProcessToolCallsAsync(
        IReadOnlyList<ToolCall> toolCalls,
        ToolAutoApprovalSettings settings,
        CancellationToken ct);

    /// <summary>
    /// Returns definitions for all 14 registered tool executors.
    /// Used by SystemPromptBuilder to construct the tool surface
    /// exposed to LLM function calling.
    /// </summary>
    IReadOnlyList<ToolDefinition> GetAvailableToolDefinitions();

    /// <summary>
    /// Checks whether a tool is enabled for the current chat.
    /// Stub: all 14 tools enabled by default.
    /// Feature 17 will implement per-chat and global tool toggles.
    /// </summary>
    bool IsToolEnabled(string toolName);

    /// <summary>
    /// Returns the current auto-approval settings, with 5 file-op
    /// tools properly categorized: reads (read_file, list_files,
    /// search_files) are auto-approved by default; writes
    /// (apply_diff, write_to_file) always require confirmation.
    /// </summary>
    ToolAutoApprovalSettings GetAutoApprovalSettings();
}
