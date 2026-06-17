# Deep Research — User Flow

## Persona
**The Hybrid Developer / Knowledge Worker / Creative Writer** — needs comprehensive, well-researched answers that go beyond a single AI response. Wants the AI to autonomously search the web, read multiple sources, and synthesize findings into a structured report with citations — all while showing real-time progress.

## Goal
Ask the AI to conduct deep research on a topic. The AI autonomously plans, searches, reads, and synthesizes — producing a structured report with inline citations. The user watches progress in real-time and can cancel at any point.

## Starting Point
The user is in an active Studio chat ([`screens/studio-chat.html`](../screens/studio-chat.html)). They have a research question that requires multiple sources and synthesis. Tool use is enabled (Tools toggle ON in the textbox toolbar).

---

## Happy Path (Complete Research → Report)

### Step 1: Trigger
**Trigger:** The user types a research request in the Studio chat textbox. This can be:
- Explicit: "Do deep research on the current state of fusion energy commercialization"
- Implicit: "Research the best Python libraries for real-time audio processing and compare them" — AI autonomously decides to use Deep Research

The user presses Enter to send the message.

### Step 2: Research Plan Formulation
The AI processes the request and formulates a research plan. This plan is displayed as a system message in the chat:

**Plan display:**
```
📋 Research Plan: "Current State of Fusion Energy Commercialization"

1. Search: "fusion energy commercial projects 2025 2026"
2. Search: "ITER progress timeline latest"
3. Search: "private fusion companies funding breakthroughs"
4. Search: "fusion energy timeline to grid"
5. Search: "fusion energy challenges remaining"
6-8. Read and extract from top sources
9. Synthesize findings
10. Produce report with citations
```

The user can see the plan before research begins. The AI starts executing immediately unless the user interrupts.

### Step 3: Execution — Search Phase
The AI begins executing the plan. Progress updates appear as system messages in real-time:

**Search progress:**
```
🔍 Searching: "fusion energy commercial projects 2025 2026" ... ✓ (4 results)
🔍 Searching: "ITER progress timeline latest" ... ✓ (6 results)
🔍 Searching: "private fusion companies funding breakthroughs" ... ✓ (5 results)
🔍 Searching: "fusion energy timeline to grid" ... ✓ (3 results)
🔍 Searching: "fusion energy challenges remaining" ... ✓ (7 results)
```

Each search result shows the query and number of results found. The user sees these appear one by one.

### Step 4: Execution — Reading Phase
The AI reads the top search results to extract relevant information:

**Reading progress:**
```
📖 Reading sources... [████████░░░░░░░░] 4 of 8
  ✓ "Fusion Energy Outlook 2025" — iter.org
  ✓ "Private Fusion Investment Hits $6B" — techcrunch.com
  → Reading: "When Will Fusion Power the Grid?" — scientificamerican.com
  ...
```

A progress bar shows overall reading progress. Each source shows its title and domain as it's read.

**If a source is inaccessible or requires authentication:** The AI skips it with annotation: "⚠️ Skipped: paywall — sciencejournal.com"

### Step 5: Execution — Synthesis Phase
After reading all sources, the AI synthesizes findings:

```
🧠 Synthesizing findings...
```

The AI integrates information from all sources, identifies consensus, notes disagreements, and structures the report.

### Step 6: Final Output — Structured Report
The AI produces the final report as a chat message. The report includes:

**Report structure:**
```markdown
# Deep Research: Current State of Fusion Energy Commercialization

## Executive Summary
[2-3 paragraph synthesis of key findings]

## Key Findings

### 1. Private Investment Surge
Private fusion companies have raised over $6B in total funding as of 2025... [1][2]

### 2. ITER Timeline
The ITER project has announced first plasma targeted for... [3]

### 3. Remaining Technical Challenges
Several key challenges remain before commercial fusion... [4][5]

## Source Analysis

### Areas of Consensus
- All sources agree that [X]...
- Timeline estimates converge around [Y]...

### Areas of Disagreement
- Source [1] claims [A], while source [6] argues [B]...

## Sources
1. "Fusion Energy Outlook 2025" — iter.org
2. "Private Fusion Investment Hits $6B" — techcrunch.com
3. ...
```

**Citations** are inline clickable links (e.g., [1]) that scroll to the Sources section. Each source shows title and domain. Actual URLs are included where available.

### Step 7: Post-Report Actions
The report appears as a standard assistant message with all normal message actions:
- **Copy MD** / **Copy Rich** — copy the report
- **⭐ Star** — favorite the message
- **Write to Wiki** — save report as a wiki file (N5)
- **Export** — export the chat with the report (I1)
- **Continue conversation** — ask follow-up questions about the report

---

## Alternative Paths

### Path B: User Cancels Mid-Research
The user clicks the **Stop** button (visible during generation) or sends a new message:

1. Research stops at the current step
2. Partial findings are preserved as system messages in the chat
3. A summary message appears: "Research paused after [N] minutes. [X] of [Y] sources processed. Partial findings are preserved above."
4. The user can:
   - Ask the AI to continue: "Continue the research from where you left off"
   - Ask a specific question about partial findings
   - Move on to a different topic

### Path C: AI Requests Clarification
Before formulating the plan, the AI may ask a clarifying question:

**AI:** "To focus my research, should I prioritize: (1) technical feasibility, (2) investment/funding landscape, or (3) regulatory/policy developments? Or cover all three broadly?"

User responds → AI adjusts the plan accordingly.

### Path D: Deep Research with Tool Auto-Approval
If Browser Search is set to "Auto-Approve" (H5), searches execute without user confirmation. If set to "Ask," each search requires user approval. Deep Research always runs with auto-approval for search operations during the research phase — the plan itself is the approval. Individual searches within the plan are auto-approved.

### Path E: Research with Wiki Integration
If the user has a personal wiki, the AI can also query the wiki (H7) as part of research:

1. AI searches wiki for "fusion energy" → finds relevant `.md` files
2. Wiki findings are incorporated alongside web search results
3. Wiki sources are cited with `[wiki: filename.md]` format
4. The user benefits from their own prior research being included

### Path F: Follow-Up Deep Research
After receiving a report, the user asks a follow-up:

**User:** "Now research the specific plasma confinement approaches and compare tokamak vs stellarator"

The AI conducts a new deep research cycle focused on the follow-up question, building on the previous report's context.

---

## Failure Points

| Failure | Handling |
|---------|----------|
| All searches return no results | "No search results found for the research queries. Try rephrasing the question or broadening the search terms." |
| All sources inaccessible (paywalls, errors) | "Could not access any sources. [N] sources were attempted but all were inaccessible (paywalls, timeouts, or errors). Try a different topic or check your internet connection." |
| AI call fails mid-research (API error) | Research stops. Partial findings preserved. Error message with **[Retry]**. |
| Network error during research | Research pauses. "Network error. Research paused after processing [X]/[Y] sources. Restore connection and click Retry to continue." **[Retry]** button. |
| Research exceeds max duration | User can set max duration in Settings → Tools. If exceeded: "Research paused after [N] minutes (limit reached). [X] of [Y] sources processed. Increase limit in Settings → Tools or continue." |
| Context window fills during research | The context overflow strategy (B8) of the active Model Configuration applies. Research continues but older chat messages may be summarized or dropped. |

---

## Edge Cases

1. **Very broad research question ("Research everything about AI"):** The AI narrows the scope in the plan: "This is a very broad topic. I'll focus on: (1) current state of AI in 2025, (2) major model providers, (3) key trends and challenges. Is this scope appropriate?" User can adjust.

2. **Research question that's a simple factual query:** The AI may respond: "This is a factual question that doesn't require deep research. [Direct answer]. Would you still like me to do a comprehensive research dive on related aspects?"

3. **Multiple concurrent deep research requests:** If the user sends a second research request while the first is still running, the second is queued. A message: "A research task is already in progress. Your new request will begin after the current one completes."

4. **Deep Research from a Tier 1 or Tier 2 interaction:** Not supported. Deep Research is only available in Tier 3 (Studio). If the user types "do deep research on X" in the Command Bar, the AI responds normally (not using the deep research pipeline).

5. **Research cites a source that later becomes unavailable:** The report includes the source title, domain, and date accessed. The content is preserved in the report. If the user clicks a citation link and the source is gone, their browser shows the standard 404 — the app can't prevent source link rot.

6. **User edits the research request after sending:** Standard message editing (D1). If edited while research is in progress, the current research is stopped and restarted with the edited request.

7. **Research produces a very long report (10,000+ words):** The report renders as a normal message with full Markdown. The user can scroll, use Chat Nav (D6) to navigate, or export the report (I1).

---

## Completion
The user has a comprehensive, cited research report directly in their chat. The report is a standard chat message — it can be copied, exported, saved to wiki, or used as the basis for further conversation. The entire research process (plan, searches, reading, synthesis) is documented in the chat as system messages, so the user can trace how the AI arrived at its conclusions.

**Typical duration:** 1-5 minutes depending on the number of sources and the AI model's speed.

**The user's primary benefit:** Autonomous multi-step research that would take a human 30-60 minutes (searching, reading, synthesizing) is completed in minutes with full traceability.

---

## Cross-References
- Feature spec: [`features/tool-use-agents.md`](../features/tool-use-agents.md) H6 — Deep Research
- Feature spec: [`features/tool-use-agents.md`](../features/tool-use-agents.md) H1 — Browser Search
- Feature spec: [`features/tool-use-agents.md`](../features/tool-use-agents.md) H7 — Wiki Search Tool
- Feature spec: [`features/tool-use-agents.md`](../features/tool-use-agents.md) H5 — Tool Auto-Approval
- Feature spec: [`features/personal-wiki.md`](../features/personal-wiki.md) N2 — Wiki Index (for H7)
- Feature spec: [`features/model-configurations-personas.md`](../features/model-configurations-personas.md) B8 — Context Overflow Strategy
- Feature spec: [`features/studio-chat-workspace.md`](../features/studio-chat-workspace.md) C5 — Stop generation
- Feature spec: [`features/message-manipulation-branching.md`](../features/message-manipulation-branching.md) D1 — Edit messages
- Screen: [`screens/studio-chat.md`](../screens/studio-chat.md) — Source and destination
