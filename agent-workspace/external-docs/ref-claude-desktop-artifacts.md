# Reference Implementation: Claude Desktop — Artifacts & Thinking Display

## Source
**Product:** Claude Desktop (Windows, Electron-based, by Anthropic)  
**Version studied:** 1.0.x (2025)  
**Component studied:** Artifacts panel, version history, diff view, thinking/reasoning display, file generation UX  

## What It Does
Claude Desktop is Anthropic's native chat application. It pioneered the "Artifacts" concept — AI-generated, versioned files (code, documents, SVGs) that appear in a side panel alongside the chat. It also pioneered the "extended thinking" display — showing the model's reasoning process before the final answer.

## Architecture (Relevant to MySecondBrain)

### Artifacts Panel
- Side panel (right of chat) shows artifacts generated during the conversation.
- Each artifact: name, type, content. Click to view in full.
- Tab-based: "Artifacts" tab and "Chat" tab in the side panel.
- Artifacts persist for the chat session; can be downloaded.
- **MySecondBrain insight:** MSB's artifacts (F1-F6) follow Claude's model with significant extensions:
  - Version history (F3) — Claude shows only the latest version; MSB maintains full version history with diff comparison.
  - Global Artifacts Browser (F7) — cross-chat artifact listing, absent in Claude.
  - Save to Wiki (F6) — bridges artifacts into the personal wiki pipeline (N5), unique to MSB.
  - Right panel layout (C37) — MSB stacks Artifacts (top) and Chat Nav (bottom) vertically with resizable divider, both collapsible. Claude uses tabs.

### Artifact Version History
- Claude: when you ask the AI to update an artifact, the new version replaces the old one. A "Version history" dropdown shows previous versions.
- Each version shows: version number, timestamp.
- No diff view between versions.
- **MySecondBrain insight:** MSB extends this with: explicit diff view (side-by-side or unified, F4), version switching with branching (F5), and the ability to revert + make changes = new branch from old version. MSB's version model is richer.

### Diff View (Claude's Approach)
- Claude does NOT provide a built-in diff view for artifact versions. You must visually compare versions.
- **MySecondBrain insight:** The mandatory Diff Viewer for wiki updates (N5 Step 5) and the artifact diff view (F4) are MSB differentiators. Claude's omission validates the user need: "I want to see exactly what changed before I commit."

### Thinking Display
- When Claude's extended thinking is enabled, a collapsible "Thinking" block appears above the response.
- During generation: "Thinking... [N]s" with a live counter. Collapsed by default.
- After generation: "Thought for [N] seconds" — the thinking content is hidden by default (privacy). User must click to reveal.
- The thinking content is rendered as plain text (no Markdown processing).
- **MySecondBrain insight:** MSB's thinking display (E3) is modeled on Claude's with two differences:
  1. MSB shows the thinking counter in real-time during streaming (Claude does too).
  2. MSB preserves the thinking content visibly after generation (collapsed by default but expandable). Claude hides it more aggressively for privacy. MSB's approach favors transparency — user can re-collapse to hide.
  3. MSB's thinking block uses monospace styling for the reasoning text (same as Claude).
  4. MSB supports thinking across any provider that offers it (OpenAI o1, DeepSeek R1, Claude) — provider-agnostic, unlike Claude's single-provider approach.

### File Generation UX
- Claude generates artifacts (code, docs, SVGs) with a "Create artifact" action.
- The artifact appears in the side panel with syntax highlighting.
- User can: copy content, download file, preview (for SVGs/HTML).
- **MySecondBrain insight:** MSB's file generation (H3) is more user-controlled: save dialog for target path, preview before save, cannot target wiki directory (N8 restriction). Claude's approach is more "auto-generate, download if you want." MSB's approach is more appropriate for a tool that writes to the user's actual file system.

## Key Takeaways for MySecondBrain

| Concept | Claude Desktop Approach | MySecondBrain Adaptation |
|---------|------------------------|-------------------------|
| Artifacts display | Side panel, tab-based | Right panel, vertically stacked (Artifacts top, Chat Nav bottom) |
| Version history | Dropdown, no diff | Full version list + side-by-side diff (DiffPlex) |
| Thinking display | Collapsed by default, monospace, timed counter | Same pattern, provider-agnostic, visible after completion |
| File generation | Auto-generate artifact → download | Save dialog → preview → confirm → write to disk |
| Artifact organization | Per-chat only | Per-chat + Global Artifacts Browser (cross-chat) |
| Save to external system | Download to file system | Save to Disk + Save to Wiki (N5 pipeline with cross-linking) |
| Diff view | Not available | Side-by-side/unified diff (F4, N5 Step 5) |

## Licensing
Proprietary (closed source). Studied for UX patterns only.

## Risk Notes
- ⚠️ Vision Flag #3: "Artifact generation mechanism needs architectural decision — how AI outputs structured artifact content vs. conversation text." Claude uses a dedicated tool-use response type for artifacts — the AI emits a special tool call that creates/updates an artifact. MSB should adopt the same pattern: artifacts are created via a tool-use response type, not by parsing markers in the message text. This aligns with how OpenAI and Anthropic structure their tool-use APIs.
- Claude's thinking display is tightly coupled to Anthropic's API. MSB's provider-agnostic thinking display needs to handle different thinking formats: Anthropic's thinking block, OpenAI's reasoning tokens (o1), DeepSeek's reasoning. Each provider may encode thinking differently in the streaming response.
- Claude does not support multi-provider comparison (MSB's Model Comparison M1-M4). This is a unique MSB feature with no direct reference implementation.
