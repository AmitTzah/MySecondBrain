# Tier 1 — Global Hotkey Text Action — User Flow

## Persona
**The Hybrid Developer / Knowledge Worker / Creative Writer** — working in any Windows application (VS Code, Word, browser, terminal), wants instant AI-powered text transformation or content generation without leaving the current application. The user may highlight text, have a text field focused, or want to capture the entire active window — depending on the Text Action they invoke.

## Goal
Press a global hotkey in any Windows application to trigger a Text Action. The app captures content per the action's capture scope, sends it to the AI with the action's system prompt and model, and applies the result per the action's apply mode — all without leaving the current application. The user can edit the result, apply it back, or elevate to Studio.

## Starting Point
MySecondBrain is running in the background (system tray or Studio window open). Global hotkeys are active. The user is in any Windows application. The required capture scope for the invoked Text Action is satisfied (e.g., for a `selection`-only action, text must be highlighted; for a `focusedElement` action, a text field must be focused; for `fullDocument`, the window must have accessible text).

---

## Happy Path A: Rewrite Highlighted Text (`selection` + `replaceSelection`)

This is the canonical Tier 1 flow — the "Rewrite" built-in default (Alt+Q). Capture highlighted text, AI rewrites it, result replaces the original.

### Step 1: Trigger
**Context:** Any Windows application with highlighted text.

The user presses **Alt+Q** (assigned to "Rewrite" Text Action). The app detects the hotkey via global keyboard hook ([`features/windows-os-integration.md`](../features/windows-os-integration.md) P1). The active Text Action's configuration is loaded: captureScope=`selection`, applyMode=`replaceSelection`, systemPrompt="Rewrite the following text to improve clarity and flow...", modelConfigId.

### Step 2: Capture Phase
**Overlay:** Minimal pill-shaped indicator near the cursor (~200×40px, non-intrusive), showing Text Action name "Rewrite."

1. Capture scope `selection` is set → app captures highlighted text via UIA TextPattern or clipboard fallback ([`features/windows-os-integration.md`](../features/windows-os-integration.md) P9.1)
2. App captures: HWND (window handle), source application name (e.g., "Code"), document/window title (if detectable, e.g., "app.ts")
3. App captures clipboard format information (DataFormats — HTML, RTF, plain text) per P4
4. "Thinking…" overlay appears near the cursor: shows animated dots or spinner
5. The captured text is sent to the AI with the Text Action's system prompt and assigned Model Configuration

**Duration:** Typically 2-10 seconds depending on text length and model speed.

### Step 3: Result Popup
**Overlay:** Expands from the "Thinking…" pill into a result popup near the cursor.

**Header:** "Rewrite" (Text Action name) + "— Code" (source app) + "— selection" (capture scope summary)

**Editable Text Area:** AI-transformed text displayed in a scrollable text area. The user can modify the text before applying it. Rendered as plain text.

**Action Buttons:**
- **[Accept]:** Triggers Phase 4 (Apply) using the action's applyMode=`replaceSelection`.
- **[Discard]:** Dismisses the popup. No changes made. Interaction saved as transient ChatThread.
- **[Open in Studio]:** Elevates to permanent ChatThread. Studio opens with result as first assistant message. Orthogonal elevation — independent of Accept/Discard.
- **[Save to Wiki]:** Opens wiki save dialog. Saves result as wiki page. Orthogonal elevation.
- **[Retry]:** Visible only if AI call failed. Re-attempts with same input and capture scope.

**Additional Instructions Field:** Text input at bottom with placeholder "Add instructions (e.g., 'make it more formal')..." User can type extra guidance and press Enter to re-run.

### Step 4: Apply Phase (on Accept)
**Apply mode:** `replaceSelection` — result replaces highlighted text in source.

1. App attempts direct HWND text injection into the source window, replacing the originally highlighted text (P3)
2. If HWND injection succeeds → text is replaced in-place. User sees new text immediately.
3. If HWND injection fails → fallback: result on clipboard (format preserved per P4), Ctrl+V simulated
4. Toast: "✓ Text applied — Rewrite" with **[Undo]** option (places original text on clipboard)
5. Popup dismisses
6. If **[Open in Studio]** was also clicked: Studio chat remains open; toast's Undo only affects source text

### Step 5: Aftermath
- Interaction saved as ChatThread (IsTransient=true if only Accept/Discard; IsTransient=false if Open in Studio clicked)
- Appears in Timeline tab
- If elevated: permanent chat in Chat list with full Studio features
- ChatThread stores: source HWND, source app name, document title, captured text, capture scope used, apply mode used

---

## Happy Path B: Continue Writing (`focusedElement` + `insertAtCursor`)

The "Continue Writing" built-in default (Alt+C). Captures entire focused textbox content, AI continues from where the text ends, result inserted at cursor.

### Step 1: Trigger
**Context:** Any Windows application with a focused text field and text content. No text needs to be highlighted.

User presses **Alt+C** ("Continue Writing"). Text Action config: captureScope=`focusedElement`, applyMode=`insertAtCursor`.

### Step 2: Capture Phase
1. Capture scope `focusedElement` is set → app identifies focused element via UIA FocusedElement, reads entire content via ValuePattern (P9.2)
2. No `selection` flag → highlighted text is irrelevant. If nothing is focused, capture fails (see Empty State)
3. App captures HWND, source app name, document title, clipboard formats (P2, P4)
4. "Thinking…" overlay: "Continue Writing — [App Name]"
5. Full textbox content sent to AI with prompt: "Continue writing from where the following text ends. Match the style, tone, and format..."

### Step 3: Result Popup
**Header:** "Continue Writing — Word — focused element text"

**Editable Text Area:** AI-generated continuation. User can edit.

**Action Buttons:** Same as Path A — [Accept], [Discard], [Open in Studio], [Save to Wiki], [Retry].

### Step 4: Apply Phase (on Accept)
**Apply mode:** `insertAtCursor` — result inserted at current cursor position in focused textbox.

1. App attempts UIA TextPattern to insert text at cursor position
2. If UIA insertion fails → fallback: result on clipboard, toast: "Result copied to clipboard — paste at cursor (Ctrl+V)"
3. Toast: "✓ Text applied — Continue Writing"

### Step 5: Aftermath
Same as Path A. ChatThread stores: captured full textbox content + AI continuation + capture scope + apply mode.

---

## Happy Path C: Summarize Page (`fullDocument` + `showOnly`)

The "Summarize Page" built-in default. Captures all accessible text in the active window, AI summarizes, result displayed in popup (no modification to source).

### Step 1: Trigger
**Context:** Any Windows application with readable text content (browser, document, PDF viewer). No text highlighting or focus required.

User invokes "Summarize Page" action (assigned hotkey or via Command Bar). Text Action config: captureScope=`fullDocument`, applyMode=`showOnly`.

### Step 2: Capture Phase
1. Capture scope `fullDocument` → app reads all accessible text via UIA DocumentRange or full tree traversal (P9.4)
2. App captures HWND, source app name, window title
3. "Thinking…" overlay: "Summarize Page — Chrome — full page text"

### Step 3: Result Popup
**Header:** "Summarize Page — Chrome — full page text"

**Editable Text Area:** AI-generated summary. User can read, edit, copy.

**Action Buttons:**
- **[Close]:** Relabeled from [Accept] for `showOnly` mode. Dismisses popup without modifying source.
- **[Discard]:** Same as Close — dismisses popup.
- **[Open in Studio]:** Elevates to permanent chat with full captured text + summary.
- **[Save to Wiki]:** Saves summary as wiki page.

### Step 4: Apply Phase (on Accept/Close)
**Apply mode:** `showOnly` — no automatic application. Popup dismissed. Interaction saved as transient ChatThread.

User may manually copy text from the editable area.

### Step 5: Aftermath
Same structure. ChatThread stores full captured document text + AI summary.

---

## Happy Path D: Explain Screen (`fullDocument + screenshot` + `showOnly`)

Combines text and visual capture. Both `fullDocument` and `screenshot` flags are set.

### Step 2: Capture Phase (distinct from other paths)
1. `fullDocument` → captures all accessible text
2. `screenshot` → captures visual screenshot of active window (P9.5)
3. Both text and image sent to AI as multimodal input
4. If screenshot fails but text succeeds → action proceeds with warning: "⚠️ Screenshot unavailable — text-only result"
5. "Thinking…" overlay: "Explain Screen — [App Name] — full page + screenshot"

### Step 3: Result Popup
**Header:** "Explain Screen — PowerPoint — full page + screenshot"

AI explanation of what's on screen, using both text content and visual layout understanding.

### Step 4: Apply Phase
`showOnly` → popup displays result. User reads, copies, or elevates.

---

## Alternative Paths (Apply Mode Variations)

### Path E: Improve Flow (`focusedElement` + `replaceFocusedElement`)
User invokes "Improve Flow" on a focused textbox. Captures entire textbox via ValuePattern. AI rewrites entire content for better flow. On Accept: replaces entire textbox content via UIA ValuePattern. Fallback: clipboard + Ctrl+A, Ctrl+V.

### Path F: Clipboard Only (any capture scope + `clipboardOnly`)
User invokes any Text Action with applyMode=`clipboardOnly`. Full capture + AI processing happens. On Accept: result copied to clipboard only. Toast: "Result copied to clipboard." Source application untouched. Useful for gathering AI-processed text to paste elsewhere.

### Path G: Append to Focused Element (`focusedElement` + `appendToFocusedElement`)
User invokes action with `appendToFocusedElement`. AI result appended to end of focused textbox. Useful for adding AI-generated sections to existing documents.

### Path H: Prepend to Focused Element (`focusedElement` + `prependToFocusedElement`)
AI result inserted at beginning of focused textbox. Useful for adding executive summaries or introductions.

---

## Alternative Paths (Interaction Variations)

### Path I: No Capturable Content
User presses hotkey but NO capture scope flags yield content.

1. "Thinking…" overlay appears briefly
2. Overlay shows: "No capturable content found. Ensure text is highlighted, a text field is focused, or the active window contains readable text."
3. Auto-dismisses after 4 seconds
4. If `selection` is the only flag and no text highlighted: "No text selected. Highlight text and try again, or edit this Text Action to use a broader capture scope."
5. No ChatThread created. Nothing saved.

### Path J: Open in Studio Without Accepting
User clicks "Open in Studio" but clicks "Discard."

1. Studio opens with conversation as permanent chat
2. Popup dismisses — no text applied to source
3. Source application unchanged
4. Studio chat ready for follow-up conversation

### Path K: Multiple Additional Instruction Rounds
User re-runs with additional instructions multiple times before deciding.

1. Each re-run uses ORIGINAL captured text + all accumulated additional instructions
2. Final edited version is applied. Previous AI responses preserved in transient ChatThread.

### Path L: Retry After Failure
AI call fails → popup shows error with **[Retry]** and **[Discard]**:
1. User can add instructions and Retry
2. Or Discard (transient thread saved with error state)
3. Or Open in Studio (elevates to permanent chat with error shown as system message)

### Path M: Source Application Closed Before Apply
User triggers action, result popup appears, but source app is closed before Accept.

1. User clicks **[Accept]**
2. HWND injection fails (window gone)
3. Fallback: result on clipboard with format preserved
4. Toast: "✓ Text copied to clipboard — [Action name]. Source application was closed."
5. No Undo option (original content may be lost)
6. Studio [Apply] button grayed out: "Source application is no longer open."

### Path N: Screenshot-Only Action
captureScope=`screenshot` only (no text flags). For non-text content (diagrams, UI mockups, images).

1. Screenshot captured of active window
2. Sent to AI as vision-only input with system prompt
3. Result displayed per applyMode
4. If screenshot fails: "Could not capture screenshot. Ensure the target window is visible and not minimized."

---

## Failure Points

| Failure | Handling |
|---------|----------|
| AI call fails (API error) | Popup shows error message with error code/details. **[Retry]** and **[Discard]** visible. **[Accept]** and **[Open in Studio]** hidden during error state. |
| Network error | Popup: "Network error. Check your connection." with **[Retry]**. |
| No capturable content (all flags empty) | "No capturable content found." Auto-dismiss after 4s. |
| `selection`-only scope, no text selected | "No text selected. Highlight text and try again, or edit this Text Action to use a broader capture scope." Auto-dismiss 4s. |
| `focusedElement` scope, no focused element | "No focused text field found. Click into a text field and try again." Auto-dismiss 4s. |
| `fullDocument` scope, no accessible text | "No readable text found in the active window." Auto-dismiss 4s. |
| `screenshot` scope fails (window minimized) | Proceeds without screenshot if text scopes also set (with warning). Fails if screenshot was sole flag. |
| HWND injection fails | Fallback to clipboard paste (Ctrl+V). Toast: "Text applied via clipboard." |
| UIA TextPattern insertion fails (`insertAtCursor`) | Fallback: result on clipboard. Toast: "Result copied to clipboard — paste at cursor." |
| UIA ValuePattern replacement fails (`replaceFocusedElement`) | Fallback: clipboard + Ctrl+A, Ctrl+V simulation. |
| Source app closed before Apply | Clipboard fallback. Toast notes source closed. No Undo. |
| Hotkey conflicts with OS/another app | Detected during hotkey configuration in Settings. Not a runtime failure. |
| Hotkey pressed but app not running | Nothing happens. Global hook only functions when MySecondBrain is running. |
| All fallbacks exhausted | Toast: "Could not apply text. Result copied to clipboard — paste manually (Ctrl+V)." |

---

## Edge Cases

1. **Very large captured content (e.g., entire document via `fullDocument`):** If text exceeds AI model's context window, truncated with "[Content truncated to [N] characters]" notice. Popup warning: "⚠️ Content was truncated — original was [N] characters. Open in Studio for full context."

2. **`screenshot` + text scopes combined:** Both text and image sent as multimodal input to AI. If the model doesn't support vision, screenshot flag is silently ignored (text-only result). The Text Action editor in Settings should indicate which models support vision.

3. **Rich text formatting in captured content:** Clipboard capture preserves format info (P4). AI receives plain text. On Apply, if source supported rich text, result pasted with original format where possible.

4. **User triggers second Tier 1 action while first is processing:** Second hotkey ignored. "Thinking…" overlay remains for first action.

5. **User triggers Tier 1 while Tier 2 Command Bar is open:** Tier 1 hotkey ignored while Command Bar active.

6. **User triggers Tier 1 while Studio is focused:** Tier 1 works from Studio too — user can highlight text in Studio textbox and invoke any Text Action.

7. **User quickly Accepts then Undos:** Toast's Undo places original/captured text on clipboard. User must manually paste. Manual undo — not automatic HWND injection.

8. **Multiple consecutive Tier 1 actions:** Each creates separate transient ChatThread. Timeline tab shows them chronologically. Full-text search (L3) finds them.

9. **Clipboard contains sensitive data:** App only reads clipboard when Tier 1 hotkey pressed (not continuously). Captured text used for AI call and stored in ChatThread. Clipboard restored after capture (P9).

10. **Capture scope mismatch with apply mode:** If `replaceSelection` is the apply mode but `selection` flag wasn't set (e.g., `focusedElement`-only scope), the apply mode degrades gracefully: `replaceSelection` → behaves as `replaceFocusedElement`. Documented as expected behavior in [`data/text-action.md`](../data/text-action.md).

11. **`showOnly` with no elevation:** User reads result and clicks Close/Discard. Transient ChatThread saved. Result not applied anywhere. User can find it later in Timeline.

12. **`clipboardOnly` with additional instructions:** User refines result via additional instructions, clicks Accept. Final refined result on clipboard. All intermediate versions in transient ChatThread.

---

## Completion

**Apply ending (modes other than `showOnly`/`clipboardOnly`):** The user's source application content has been transformed per the Text Action. Brief toast confirms. User continues working in original application. Interaction saved as transient ChatThread.

**Clipboard ending (`clipboardOnly`):** Result on clipboard. User pastes wherever needed. Toast: "Result copied to clipboard."

**Show-only ending (`showOnly`):** User reviews result in popup, may copy or elevate. Popup dismissed. Content not applied to source.

**Elevation ending:** User clicked "Open in Studio" — permanent ChatThread created. Studio open with conversation ready for follow-up. Source text may or may not have been updated (depending on Accept).

**The user's primary benefit:** Zero context-switching. The entire interaction — capture, AI processing, review, apply — happens without leaving the original application. The AI transformation appears as a native feature of whatever app they're using, with capture scope and apply mode customized per action.

---

## Cross-References
- Feature spec: [`features/text-actions-three-tier.md`](../features/text-actions-three-tier.md) K1 — Text Action definition (three dimensions), K3 — Tier 1 three-phase flow
- Feature spec: [`features/windows-os-integration.md`](../features/windows-os-integration.md) P1-P4, P9 — Global hooks, HWND capture, spatial anchoring, clipboard, UIA context capture
- Feature spec: [`features/data-model-lifecycle.md`](../features/data-model-lifecycle.md) O1-O4 — ChatThread model, IsTransient, elevation, auto-cleanup
- Feature spec: [`features/model-configurations-personas.md`](../features/model-configurations-personas.md) B2 — Model Configurations used by Text Actions
- Data entity: [`data/chat-thread.md`](../data/chat-thread.md) — ChatThread with HWND context
- Data entity: [`data/message.md`](../data/message.md) — Messages
- Data entity: [`data/text-action.md`](../data/text-action.md) — Text Action definitions with captureScope and applyMode
- Screen: [`screens/studio-chat.md`](../screens/studio-chat.md) — Destination for Open in Studio elevation
- Screen: [`screens/settings.md`](../screens/settings.md) — Hotkey configuration, Text Action editor
