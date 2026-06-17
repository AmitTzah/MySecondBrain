# User Personas

## Primary Persona

**The Hybrid Developer / Knowledge Worker / Creative Writer**

A technically proficient professional who uses AI as an integral part of three distinct workflows:

- **Software Development:** Writes code daily. Uses AI for code generation, debugging, refactoring, architecture discussion, and learning new technologies. Works primarily in VS Code, terminal, and browsers. Wants AI assistance instantly available from any application via hotkeys — highlight code, hit a shortcut, get a rewrite without leaving the editor.

- **Knowledge Management:** Invests heavily in personal knowledge synthesis. Discusses concepts with AI, takes structured notes, and builds a personal reference library of polished .md files. Already maintains example wiki entries in the project directory (driving.md, git-cheatsheet.md) that were co-created with AI. Wants the "write to wiki" workflow to eliminate the friction of manually copying AI output into note files.

- **Creative Writing:** Authors a webnovel using a self-built Microsoft Word add-in that generates text with AI assistance. Needs the WebSocket bridge for direct integration between MySecondBrain's AI capabilities and the Word add-in pipeline. Wants format-preserving clipboard handling (HTML/RTF) when pushing AI-generated text into rich-text documents.

**Goals:**
- Replace all scattered AI chat platforms (ChatGPT.com, Claude.ai, etc.) with a single Windows-native interface
- Eliminate all copy-paste friction between AI output and actual work (code, notes, novel)
- Build a growing, searchable personal wiki from AI-assisted discussions
- Have AI assistance instantly available from any Windows application via system-wide hotkeys
- Keep all AI interactions organized, searchable, and under the user's control (own API keys, own data)

**Frustrations with Current Solutions:**
- No full-text search across past chats on any major platform
- No chat favoriting or organizational features
- Cannot configure different system messages for different chats
- Provider lock-in — conversations scattered across ChatGPT, Claude, and others
- Web-based interfaces force manual copy-paste between AI output and actual work
- AI conversations are dead ends — no path from "discussing a topic" to "saving what I learned"

## Secondary Personas

Not applicable — MySecondBrain is a single-user personal tool. There are no other user types, roles, or personas.

## Non-Users

- Anyone who does not use Windows as their primary operating system
- Anyone who prefers web-based AI chat interfaces and is unwilling to configure their own API keys
- Anyone unwilling to manage or connect to local open-source models
