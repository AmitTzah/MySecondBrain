# Tier 1 — Global Hotkey Text Rewrite — User Flow

## Persona
**The Hybrid Developer / Knowledge Worker / Creative Writer** — working in any Windows application (VS Code, Word, browser, terminal), highlights text, and wants an instant AI-powered text transformation without leaving the current application.

## Goal
Highlight text in any Windows application, press a global hotkey, and receive an AI-transformed version of the text — with the ability to edit the result, apply it back to the source application, or elevate the interaction to a full Studio conversation.

## Starting Point
The user is in any Windows application (e.g., VS Code editing code, Word writing prose, a browser composing an email). The user has highlighted text. MySecondBrain is running in the background (system tray or Studio window open). Global hotkeys are active.

---

## Happy Path (Rewrite with Accept)

### Step 1: Trigger
**Context:** Any Windows application with highlighted text.

The user presses a global hotkey assigned to a Text Action (e.g., **Alt+Q** for "Rewrite"). The app detects the hotkey via global keyboard hook ([`features/windows-os-integration.md`](../features/windows-os-integration.md) P1).

### Step 2: Capture Phase
**Overlay:** Minimal pill-shaped indicator near the cursor (~200×40px, non-intrusive).

1. App captures the highlighted text from the active window via clipboard or UI Automation
2. App captures: HWND (window handle), source application name (e.g., "Code"), document/window title (if detectable, e.g., "app.ts")
3. App captures clipboard format information (DataFormats — HTML, RTF, plain text) per [`features/windows-os-integration.md`](../features/windows-os-integration.md) P4
4. "Thinking…" overlay appears near the cursor: shows animated dots or spinner
5. The captured text is sent to the AI with the Text Action's system prompt (e.g., "Rewrite the following text to improve clarity and flow while preserving meaning...") and the assigned Model Configuration

**Duration:** Typically 2-10 seconds depending on text length and model speed.

### Step 3: Result Popup
**Overlay:** Expands from the "Thinking…" pill into a result popup near the cursor.

**Header:** "Rewrite" (Text Action name) + "— Code" (source application name)

**Editable Text Area:** AI-transformed text displayed in a scrollable text area. The user can modify the text before applying it. Rendered as plain text (no Markdown — this is a text transformation, not a chat).

**Action Buttons:**
- **[Accept]:** Triggers Phase 4 (Apply). This is the primary happy-path action.
- **[Discard]:** Dismisses the popup. No changes made to the source application. The captured text and AI response are saved as a transient ChatThread (IsTransient=true) for the Timeline tab. Subject to 7-day auto-cleanup.
- **[Open in Studio]:** Elevates the interaction to a permanent ChatThread. The Studio window opens/focuses with a new tab showing the captured text as the user message and the AI response as the assistant message. The chat is permanent (IsTransient flipped to false). The popup remains open — user can still Accept or Discard independently.
- **[Retry]:** Visible only if the AI call failed (not normally visible on success). Re-attempts with same input.

**Additional Instructions Field:** Text input at the bottom of the popup with placeholder "Add instructions (e.g., 'make it more formal')..." The user can type extra guidance and press Enter to re-run the Text Action with the original text + additional instruction appended. The result popup updates with the new AI response.

**Decision point — Edit then Accept:** The user edits the AI-transformed text in the editable area, then clicks Accept. The edited version (not the original AI output) is applied.

**Decision point — Additional Instructions:** The user types "make it sound more technical" and presses Enter. The AI re-processes with the combined prompt. New result replaces the previous one. User can Accept, keep editing, or add more instructions.

**Decision point — Open in Studio then Accept:** The user clicks "Open in Studio" first (Studio opens with the conversation), then clicks "Accept" (text applied to source). These are independent actions — both happen.

### Step 4: Apply Phase (on Accept)
**Behavior:** Pushes the result back to the source application.

1. App attempts direct HWND text injection into the source window, replacing the originally highlighted text ([`features/windows-os-integration.md`](../features/windows-os-integration.md) P3)
2. If HWND injection succeeds → text is replaced in-place. The user sees the new text in their original application immediately.
3. If HWND injection fails → fallback: result placed on clipboard (format preserved — HTML, RTF, or plain text per P4), then Ctrl+V simulated to paste
4. Brief confirmation toast appears: "✓ Text applied — Rewrite" with **[Undo]** option (places original text back on clipboard so user can paste to revert)
5. Popup dismisses
6. If **[Open in Studio]** was also clicked: the Studio chat remains open and unaffected by Accept/Discard. The toast's Undo only affects the source application text, not the Studio chat.

### Step 5: Aftermath
- The interaction is saved as a ChatThread (IsTransient=true if only Accept/Discard; IsTransient=false if Open in Studio was clicked)
- The interaction appears in the Timeline tab ([`screens/studio-chat.html`](../screens/studio-chat.html) sidebar → Timeline)
- If elevated via Open in Studio: the chat appears in the main Chat list as a permanent chat. The user can continue the conversation in Studio with full features (C section).
- The ChatThread stores: source HWND, source app name, document title, original highlighted text (for the [Apply] button in Studio if user returns later — see C5a)

---

## Alternative Paths

### Path B: No Text Selected
The user presses the hotkey without highlighting any text.

1. "Thinking…" overlay appears briefly
2. Overlay shows: "No text selected. Highlight text in any application and try again."
3. Auto-dismisses after 3 seconds
4. No ChatThread created. Nothing saved.

### Path C: Open in Studio Without Accepting
The user clicks "Open in Studio" but clicks "Discard" (not "Accept").

1. Studio opens with the conversation as a permanent chat
2. Popup dismisses — no text applied to source application
3. The source application is unchanged
4. The Studio chat is ready for follow-up conversation

### Path D: Multiple Additional Instruction Rounds
The user types additional instructions and re-runs multiple times before deciding:

1. First result appears → user types "make it shorter" → Enter → second result
2. User types "now make it more technical" → Enter → third result
3. User edits the third result manually → clicks Accept
4. Each re-run uses the ORIGINAL captured text + all accumulated additional instructions
5. The final edited version is applied. Previous AI responses are preserved in the transient ChatThread.

### Path E: Retry After Failure
The AI call fails → popup shows error message with **[Retry]** and **[Discard]** buttons:
1. User can type additional instructions and click Retry (re-attempts with same input + new instructions)
2. Or user can click Discard (dismisses, transient thread saved with error state)
3. Or user can click Open in Studio (elevates to permanent chat — the error is shown as a system message; user can retry from Studio with full chat features)

### Path F: Source Application Closed Before Apply
The user triggers the rewrite, the result popup appears, but before clicking Accept, the user closes the source application (e.g., closes VS Code).

1. User clicks **[Accept]**
2. HWND injection fails (window no longer exists)
3. Fallback: result placed on clipboard with format preserved
4. Toast: "✓ Text copied to clipboard — Rewrite. Source application was closed." 
5. No **[Undo]** option (original text may be lost — the app no longer has the window context to restore)
6. If the chat was elevated to Studio: the **[Apply]** button in the chat header is grayed out with tooltip "Source application 'Code — app.ts' is no longer open." The copy buttons (Copy MD, Copy Rich) remain functional.

---

## Failure Points

| Failure | Handling |
|---------|----------|
| AI call fails (API error) | Popup shows error message with error code/details. **[Retry]** and **[Discard]** visible. **[Accept]** and **[Open in Studio]** hidden during error state. |
| Network error | Popup shows: "Network error. Check your connection." with **[Retry]**. |
| No text selected | "No text selected. Highlight text in any application and try again." Auto-dismiss after 3s. |
| HWND injection fails | Fallback to clipboard paste (Ctrl+V simulation). Toast: "Text applied via clipboard." |
| HWND injection fails + clipboard fallback also fails | Toast: "Could not apply text. Result copied to clipboard — paste manually (Ctrl+V)." |
| Source app closed before Apply | Clipboard fallback. Toast notes source was closed. No Undo available. |
| Hotkey conflicts with OS/another app | Detected during hotkey configuration in Settings. Not a runtime failure. |
| Hotkey pressed but app is not running | Nothing happens. The global keyboard hook only functions when MySecondBrain is running. |

---

## Edge Cases

1. **Very long highlighted text (e.g., entire document):** If the text exceeds the AI model's context window, the text is truncated with "[Text truncated to [N] characters]" notice. The user is warned in the popup: "⚠️ Text was truncated — the original was [N] characters. Open in Studio for full context."

2. **Highlighted text includes formatting (Word rich text):** The clipboard capture preserves format information (P4). The AI receives plain text. On Apply, if the source supported rich text, the result is pasted with the original format (RTF/HTML) where possible. If only plain text was available, plain text is returned.

3. **User triggers a second Tier 1 action while the first is still processing:** The second hotkey press is ignored while a Tier 1 popup is active. The "Thinking…" overlay remains for the first action.

4. **User triggers Tier 1 while Tier 2 Command Bar is open:** Tier 1 hotkey is ignored while Command Bar is active. Command Bar takes priority.

5. **User triggers Tier 1 while Studio is the focused window:** Tier 1 works from Studio too — the user can highlight text in the Studio textbox and press Alt+Q to rewrite their own message draft before sending.

6. **User quickly Accepts then immediately Undos:** The toast's Undo places the original text back on clipboard. The user must manually paste (Ctrl+V) to restore. This is a manual undo — not automatic HWND injection of the original.

7. **Multiple consecutive Tier 1 actions:** Each creates a separate transient ChatThread. The Timeline tab shows them in chronological order. If the user later searches for "that rewrite I did on the login form," full-text search (L3) will find it.

8. **Clipboard contains sensitive data from another app:** The app only reads clipboard text when a Tier 1 hotkey is pressed (not continuously). The captured text is used for the AI call and stored in the ChatThread.

---

## Completion
**Happy path ending:** The user's highlighted text in the source application has been replaced with the AI-transformed version. A brief toast confirms the action. The user continues working in their original application. The interaction is saved as a transient ChatThread, visible in the Timeline tab.

**Elevation ending:** The user clicked "Open in Studio" — a permanent ChatThread is created with the captured text and AI response. The Studio window is open with this conversation ready for follow-up. The source application text may or may not have been updated (depending on whether Accept was also clicked).

**The user's primary benefit:** Zero context-switching. The entire interaction — highlight, hotkey, review, apply — happens without leaving the original application. The AI transformation appears to be a native feature of whatever app they're using.

---

## Cross-References
- Feature spec: [`features/text-actions-three-tier.md`](../features/text-actions-three-tier.md) K3 — Tier 1 three-phase flow
- Feature spec: [`features/windows-os-integration.md`](../features/windows-os-integration.md) P1-P4 — Global hooks, HWND capture, spatial anchoring, clipboard
- Feature spec: [`features/data-model-lifecycle.md`](../features/data-model-lifecycle.md) O1-O4 — ChatThread model, IsTransient, elevation, auto-cleanup
- Feature spec: [`features/model-configurations-personas.md`](../features/model-configurations-personas.md) B2 — Model Configurations used by Text Actions
- Data entity: [`data/chat-thread.md`](../data/chat-thread.md) — ChatThread with HWND context
- Data entity: [`data/message.md`](../data/message.md) — Messages
- Data entity: [`data/text-action.md`](../data/text-action.md) — Text Action definitions
- Screen: [`screens/studio-chat.md`](../screens/studio-chat.md) — Destination for Open in Studio elevation
- Screen: [`screens/settings.md`](../screens/settings.md) — Hotkey configuration
