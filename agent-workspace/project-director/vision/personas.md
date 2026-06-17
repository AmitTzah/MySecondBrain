# User Personas

## Primary Persona

**The Hybrid Developer / Knowledge Worker / Creative Writer**

A technically proficient professional who uses AI as an integral part of three distinct workflows:

- **Software Development:** Writes code daily. Uses AI for code generation, debugging, refactoring, architecture discussion, and learning new technologies. Works primarily in VS Code, terminal, and browsers. Wants AI assistance instantly available from any application via hotkeys — highlight code, hit a shortcut, get a rewrite without leaving the editor.

- **Knowledge Management:** Invests heavily in personal knowledge synthesis. Discusses concepts with AI, takes structured notes, and builds a personal reference library of polished .md files. Wants the "Write to Wiki" workflow to eliminate the friction of manually copying AI output into note files. Already maintains example wiki entries that were co-created with AI.

- **Creative Writing:** Authors a webnovel using a self-built Microsoft Word add-in that generates text with AI assistance. Needs the WebSocket bridge for direct integration between MySecondBrain's AI capabilities and the Word add-in pipeline. Wants format-preserving clipboard handling (HTML/RTF) when pushing AI-generated text into rich-text documents.

### Goals

1. **Complete Platform Replacement:** Replace all scattered AI chat platforms (ChatGPT.com, Claude.ai, and all others) with a single Windows-native interface. The goal is to never open another AI chat website again — MySecondBrain becomes the exclusive interface for all AI interactions.

2. **Eliminate Copy-Paste Friction:** Remove all manual copying and pasting between AI output and actual work — whether that's code in VS Code, notes in Markdown files, or prose in Word. Tier 1 hotkey rewrites and the "Write to Wiki" pipeline should eliminate this friction entirely.

3. **Build a Personal Wiki:** Grow a searchable, indexed personal knowledge base from AI-assisted discussions. Every meaningful AI conversation should have a path to becoming a permanent, polished .md note on disk.

4. **Instant AI Access Anywhere:** Have AI assistance available from any Windows application via system-wide hotkeys. No context-switching to a browser tab — highlight text, press a hotkey, get a result.

5. **Full Control & Organization:** Keep all AI interactions organized, searchable, and under the user's complete control — own API keys, own data, own files on disk. Nothing locked into someone else's platform.

### Frustrations with Current Solutions

- **No Full-Text Search:** No major AI chat platform provides full-text search across all past conversations. Finding "that thing I discussed three weeks ago" requires manually scrolling through chat history.

- **No Organizational Features:** No chat favoriting, tagging, pinning, or folder organization on ChatGPT, Claude, or other platforms. All chats are a flat chronological list.

- **Cannot Configure Per-Chat System Messages:** Most platforms require starting a new "Custom GPT" or project to change the system prompt. There's no lightweight way to say "this chat uses the Python expert persona, that chat uses the writing coach persona."

- **Provider Lock-In:** Conversations are scattered across ChatGPT, Claude, Gemini, and others. Each platform has different features, different conversation histories, and different limitations. There's no unified interface.

- **Manual Copy-Paste Tax:** Web-based AI interfaces force the user to copy AI output, switch to the target application, and paste — then reformat. This friction adds up to significant time waste across hundreds of interactions.

- **Conversations Are Dead Ends:** Current AI chat platforms have no path from "discussing a topic" to "saving what I learned." The conversation ends, and the knowledge stays trapped in chat history — or requires manual effort to extract and organize.

## Secondary Personas

Not applicable — MySecondBrain is a single-user personal tool. There are no other user types, roles, or personas. The application will never support multi-user functionality (see feature-inventory.md → Explicitly Out of Scope).

## Non-Users

- Anyone who does not use Windows as their primary operating system. The app is Windows-only and will never support macOS, Linux, iOS, Android, or web-based access.
- Anyone who prefers web-based AI chat interfaces and is unwilling to configure their own API keys.
- Anyone unwilling to manage or connect to their own AI provider accounts. MySecondBrain requires the user to bring their own API keys.
