# Tool Use (Agent Capabilities) — Feature Spec

## What the User Accomplishes
The AI can act as an agent: performing web searches, executing terminal commands (with mandatory user approval), creating/editing files on disk, and conducting multi-step deep research. The user controls which tools auto-approve and which require confirmation.

## Trigger
- AI requests tool use during conversation
- User explicitly asks AI to use a tool ("Search the web for...")
- User invokes Deep Research (H6)

## Detailed Behavior

### H1. Browser Search
- AI requests web search → app executes search → feeds results back to AI
- User sees what search was performed (search query displayed as system message)
- **Implementation Options (Architect decision):**
  - Opens default browser with search query
  - Uses search API (e.g., Bing, Google)
  - ⚠️ FLAGGED: Search implementation approach needs architectural decision.

### H2. Terminal/Script Execution
- AI requests execution of shell command or script
- **ALWAYS requires explicit user confirmation** — cannot be auto-approved (overrides H5)
- Confirmation dialog shows: exact command to be executed, working directory, risk level
- User clicks "Approve" → command runs in terminal → output captured → fed back to AI
- User clicks "Deny" → AI notified that execution was denied
- **Security:** Commands run with user's permissions. No sandboxing (Architect decision).
- ⚠️ FLAGGED: Terminal execution security model. Running arbitrary AI-suggested commands is high-risk. Consider sandboxing, command allowlisting, or dry-run preview.

### H3. File Generation
- AI creates new files on disk
- User specifies or approves target path via save dialog
- AI provides file content; app writes to disk
- **Restriction:** Cannot target wiki directory (N8)

### H4. File Editing
- AI modifies existing files on disk
- User approves target file (file picker)
- User can review changes before applying
- **Restriction:** Cannot target wiki directory (N8)

### H5. Tool Auto-Approval
- **Global Defaults:** Settings → Tools. Configure which tool types auto-execute:
  - Browser Search: Auto-Approve / Ask / Disabled
  - File Generation: Auto-Approve / Ask / Disabled
  - File Editing: Auto-Approve / Ask / Disabled
  - Terminal Execution: ALWAYS Ask (cannot auto-approve, H2 overrides)
- **Per-Chat Override:** Textbox toolbar dropdown. Overrides global defaults for current chat.
- **Auto-Approve Indicator:** When tools auto-approved, subtle indicator in chat: "Tools: Auto" vs "Tools: Ask"

### H6. Deep Research
- Invoked by user request in chat (e.g., "Do deep research on...")
- **Process:**
  1. AI formulates research plan (displayed to user)
  2. AI performs multiple web searches
  3. AI reads and extracts information from source pages
  4. AI synthesizes findings
  5. AI produces structured report with inline citations
- **Progress Display:** Real-time status updates in chat:
  - "Searching..." (spinner)
  - "Reading 3 of 8 sources..." (progress bar)
  - "Synthesizing..." (spinner)
- **Final Output:** Report appears as chat message with clickable citations
- **Duration:** May take several minutes
- **Cancellation:** User can cancel at any point; partial findings preserved

### H7. Wiki Search Tool
- AI can query user's local wiki index (N2) to find relevant .md files
- AI sees matching file names, headings, and content snippets
- AI incorporates findings into responses
- **Trigger:** AI autonomously decides to search wiki, OR user asks "What did I write about X?"
- **Zero API Cost:** Wiki search is purely local (queries the local index)
- **Restrictions:** AI can only READ wiki content, not modify (N8)

## Data
- Tool call records stored with Messages (what tool was called, parameters, result)
- Deep Research: research plan + intermediate findings stored

## Success/Failure States
- **Terminal Denied:** AI notified: "Command execution was denied by the user."
- **Terminal Failed:** Output with error code shown to AI: "Command failed with exit code [N]. Output: [stderr]"
- **Search Failed:** "Web search failed. [error]"
- **File Write Failed:** "Could not write to [path]. Check permissions."
- **Deep Research Timeout:** User can set max duration. If exceeded: "Research paused after [N] minutes. [N] of [M] sources processed."

## Permissions
- H2 (terminal) ALWAYS requires confirmation — hard-coded rule
- H5 (auto-approval) configurable globally and per-chat
- H3/H4 restricted from wiki directory (N8)

## Interactions
- H7 queries N2 (wiki index)
- H3/H4 restricted by N8 (wiki access restrictions)
- H5 settings stored with Persona/chat preferences
