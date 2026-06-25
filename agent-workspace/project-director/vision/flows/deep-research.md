# Deep Research — User Flow (Skill-Based)

## Persona
**The Hybrid Developer / Knowledge Worker / Creative Writer** — needs comprehensive, well-researched answers that go beyond a single AI response. Wants the AI to autonomously search the web, read multiple sources, and synthesize findings into a structured report with citations.

## Goal
Ask the AI to conduct deep research on a topic. The AI follows a skill-based research protocol using `web_search`, `web_fetch`, and `bash` tools. Progress is visible naturally as tool calls stream in chat — no custom state machine or progress UI. The final output is a structured report with inline citations, optionally presented as an artifact via `present_files`.

## Starting Point
The user is in an active Studio chat ([`screens/studio-chat.html`](../screens/studio-chat.html)). They have a research question that requires multiple sources and synthesis. The Deep Research skill is enabled in the Skills dropdown, and web_search + web_fetch tools are enabled in the Tools dropdown.

---

## Happy Path (Complete Research → Report)

### Step 1: Trigger
**Trigger:** The user types a research request in the Studio chat textbox:
- Explicit: "Do deep research on the current state of fusion energy commercialization"
- Implicit: "Research the best Python libraries for real-time audio processing and compare them"

The model recognizes the research task matches the Deep Research skill description → calls `skill_load("deep-research")` → the skill's instructions enter context.

**No custom progress UI.** The user sees the skill_load tool call appear: "📚 Loaded skill: deep-research."

### Step 2: Research Plan
The model formulates a research plan (following the skill's instructions) and communicates it transparently:
> "I'll research fusion energy commercialization. Plan: (1) search for recent breakthroughs, (2) identify major companies and funding, (3) read key articles, (4) synthesize findings into a report with citations."

### Step 3: Multi-Source Search
The model calls `web_search` multiple times with different query angles to gather diverse sources:
- `web_search("fusion energy breakthroughs 2025")` → 10 results appear as tool call
- `web_search("private fusion investment funding 2025")` → 10 results
- `web_search("ITER project progress timeline")` → 10 results

The user sees each search query and result count in real-time as tool calls stream.

### Step 4: Source Reading
The model selects the most promising URLs from search results and calls `web_fetch` on each. Per the URL constraint (H8), it can only fetch URLs seen in prior search results.

- `web_fetch("https://iter.org/fusion-outlook-2025")` → page content extracted
- `web_fetch("https://techcrunch.com/fusion-investment-6b")` → page content extracted
- `web_fetch("https://nature.com/fusion-milestones-2025")` → page content extracted

The user sees each fetch with a brief status indicator.

### Step 5: Synthesis
The model synthesizes all gathered information into a structured report with inline citations. It writes the report to the workspace using `write_to_file`:
- `write_to_file("fusion-energy-research.md", "[Report content with [1], [2], [3] citations]")`

### Step 6: Present Report
The model calls `present_files(["fusion-energy-research.md"])` → the report appears in the WebView2 artifacts side panel with:

- Full Markdown rendering
- Clickable citation markers that scroll to the Sources section
- "Save to Disk" and "Save to Wiki" buttons
- Syntax-highlighted code blocks (if any)
- Rendered tables, lists, and formatted text

The Sources section at the bottom of the report lists each citation with title, domain, and date-accessed:
```
## Sources
[^1]: "Fusion Energy Outlook 2025" — iter.org — accessed 2026-06-15
[^2]: "Private Fusion Investment Hits $6B" — techcrunch.com — accessed 2026-06-15
[^3]: "Fusion Milestones 2025" — nature.com — accessed 2026-06-15
```

### Step 7: Iteration (Optional)
User can request refinements: "Add a section on regulatory challenges." The model uses `apply_diff` on the same file → `present_files` again → the app creates v2 of the artifact.

---

## Alternative Paths

### Quick Research (Simple Question)
For simple factual questions, the model may skip the full research protocol and just do one `web_search` + direct answer. The skill instructions guide the model on when to use full protocol vs. quick search.

### Research Without Artifact
If the research is conversational and the user doesn't need a saved report, the model may present findings directly in chat without creating a file artifact. The skill instructions let the model decide based on context.

### Research Into Existing Wiki File
If the user says "Research X and add it to my existing Y.md wiki file," the model:
1. Conducts research (search + fetch)
2. Synthesizes findings
3. Uses the Write to Wiki pipeline (N5) instead of `present_files`
4. The existing wiki file is updated with new research content

---

## Failure Points

| Failure | Behavior |
|---------|----------|
| **web_search API key exhausted** | "Web search failed: API quota exceeded. Check your search API key in Settings." Model offers to continue with whatever sources were gathered. |
| **web_fetch timeout on source page** | "Could not fetch [URL]: Request timed out." Model skips that source and continues with others. |
| **User cancels mid-research (Stop button)** | Model stops all in-progress tool calls. Partial findings preserved in conversation. If partial report was written to workspace, it remains but is not presented. |
| **No relevant search results found** | "Search for '[query]' returned no relevant results." Model suggests broader search terms or alternative approaches. |
| **Deep Research skill not enabled** | Model cannot load skill instructions. May still do basic web_search but won't follow the research protocol. Quality may be lower. |
| **All web_fetch calls fail** | Model reports: "I was unable to access any of the source pages. Here's what I found from search snippets alone..." |
| **Context window fills during research** | Context compaction triggers. Skill content blocks are protected from pruning (W10). Older search results may be summarized. Research continues. |

---

## Screens Involved

| Step | Screen | What User Sees |
|------|--------|---------------|
| 1 | [`studio-chat.html`](../screens/studio-chat.html) | User types research request in textbox |
| 2 | [`studio-chat.html`](../screens/studio-chat.html) | `skill_load` tool call: "📚 Loaded skill: deep-research" |
| 3 | [`studio-chat.html`](../screens/studio-chat.html) | `web_search` tool calls with query and result count, streaming in real-time |
| 4 | [`studio-chat.html`](../screens/studio-chat.html) | `web_fetch` tool calls with URL and content preview |
| 5 | [`studio-chat.html`](../screens/studio-chat.html) | `write_to_file` tool call: "✍️ write_to_file: fusion-energy-research.md" |
| 6 | [`studio-chat.html`](../screens/studio-chat.html) | `present_files` tool call: "📄 Presented: fusion-energy-research.md." Artifact appears in WebView2 side panel. |
| 7 | [`studio-chat.html`](../screens/studio-chat.html) | Refinement iterations via `apply_diff` + `present_files` |

---

## Data Involved

- [`data/chat-thread.md`](../data/chat-thread.md) — the chat conversation
- [`data/message.md`](../data/message.md) — tool call messages (web_search, web_fetch, write_to_file, apply_diff, present_files)
- [`data/artifact.md`](../data/artifact.md) — the presented research report with version history
- No custom data entities needed — research is built entirely on existing tool infrastructure

## Cross-References

- Tool specs: [`features/tool-use-agents.md`](../features/tool-use-agents.md) §H7 (web_search), §H8 (web_fetch), §H6 (bash), §H4 (apply_diff), §H5 (write_to_file), §H13 (present_files), §H12 (skill_load)
- Skill spec: [`features/agent-skills.md`](../features/agent-skills.md) §W9 (Deep Research as Skill)
- Artifacts: [`features/artifacts-side-panel.md`](../features/artifacts-side-panel.md) §F1 (workspace-to-artifact pipeline)
- Citation rendering: [`planning/abstractions.md`](../../planning/abstractions.md) §CitationRenderer
