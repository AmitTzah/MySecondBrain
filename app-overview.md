# App Overview

## Core Purpose

MySecondBrain is a native Windows desktop application that serves as the user's sole interface for all AI language model interactions — replacing and extending ChatGPT.com, Claude.ai, and all other LLM chat platforms. Its core purpose is twofold:

**1. Unified, Provider-Agnostic Chat Hub:** The app consolidates all LLM conversations into a single Windows-native interface. It supports any API key from any provider and local open-source models, eliminating provider lock-in and scattered conversations. It provides QoL features absent from web-based competitors: full-text search across all past chats, chat favoriting, per-chat system message configuration, and more.

**2. Personal Wiki / Second Brain:** Beyond chat, the app maintains an indexed, searchable personal knowledge base of user-authored .md files stored in a user-chosen directory on disk. The app watches this directory and indexes its contents. Files remain plain .md — fully editable by external tools like VS Code. An AI agent can read a chat conversation and produce or update a polished .md summary file, enabling the user to discuss a concept with an AI, collaboratively refine notes, and permanently store the result in their wiki.

The app operates across three interaction tiers: ephemeral hotkey-triggered actions (Tier 1), a Spotlight-style command bar with mini-chat (Tier 2), and a full Studio chat workspace (Tier 3). All interactions — regardless of tier — are captured as ChatThreads in a unified data model, with transient threads auto-cleaning after 7 days and permanent threads preserved indefinitely.

## Elevator Pitch

MySecondBrain is a Windows desktop app that replaces all your scattered AI chat tools — ChatGPT, Claude, and others — with a single, provider-agnostic hub where you can use any AI model you want. It goes beyond chat by serving as your personal wiki: discuss a topic with AI, and it helps you create polished, permanent .md notes stored on your own computer. With three tiers of interaction — from instant hotkey rewrites to deep Studio conversations — everything you do is captured, searchable, and never locked into one company's platform.

## Key Differentiators

1. **Three-Tier Interaction Model** — Unlike every other AI chat tool which offers only a single chat window, MySecondBrain provides three tiers: instant hotkey-triggered actions appearing as a minimal overlay at the cursor (Tier 1), a Spotlight-style command bar that expands into a mini-chat (Tier 2), and a full Studio workspace for deep conversations (Tier 3). The user moves fluidly between tiers, and interactions from any tier are seamlessly elevated to permanent chats.

2. **Provider-Agnostic by Design** — The app is not locked to any single AI provider. The user supplies their own API keys for any provider and can connect to local open-source models. No subscription, no platform lock-in.

3. **Integrated Personal Wiki** — Chat conversations are not dead ends. A "write to wiki" action uses AI to produce polished .md summary files from chat discussions. These files live on the user's own disk as plain .md, indexed and searchable within the app, and editable by any external tool.

4. **Windows-Native Deep OS Integration** — Being Windows-only enables capabilities no web-based app can offer: global keyboard hooks for system-wide hotkeys, HWND capture for spatial anchoring (pushing revised text back into the original application window), clipboard format preservation (respecting HTML/RTF source formats), and a local WebSocket server for direct integrations.

5. **Unified Data Model with Automatic Cleanup** — Every interaction, from a 2-second hotkey rewrite to a 2-hour deep conversation, is captured as a ChatThread in the same data model. Transient interactions auto-clean after 7 days; permanent chats are preserved indefinitely. Nothing is lost, nothing bloats.

## Platform

MySecondBrain is a native Windows desktop application only. It is NOT a web application, NOT a mobile application, and NOT cross-platform. It targets Windows 10 and Windows 11 exclusively. Being Windows-native enables deep OS integration: global keyboard hooks, HWND capture, clipboard interception, and local WebSocket server — capabilities that are impossible or severely limited in web-based or cross-platform frameworks.

## Success Metrics

As a single-user personal tool, MySecondBrain's success is measured by the degree to which it replaces the user's previous AI workflows and becomes indispensable. The following metrics define success:

1. **Usage Replacement:** The user stops visiting ChatGPT.com, Claude.ai, and other LLM platforms entirely. MySecondBrain becomes the exclusive interface for all AI interactions. Success means zero reliance on any other AI chat interface.

2. **Wiki Growth:** The personal wiki directory grows steadily over time as the user captures knowledge from AI-assisted discussions. Success means the wiki becomes the user's primary reference for everything they've learned — a genuine "second brain."

3. **Workflow Speed:** Measurable elimination of copy-pasting between AI chats and other tools. Success means the user no longer manually copies AI output into note files or editors — the app's integrated workflows (Tier 1 hotkey rewrites, Tier 2 quick queries, "write to wiki") eliminate this friction entirely.

4. **Feature Adoption:** The user regularly uses all three interaction tiers, not just the Studio chat. Success means Tier 1 (hotkey rewrites) and Tier 2 (command bar) become muscle-memory habits, and the user naturally elevates useful transient interactions to permanent Studio chats.
