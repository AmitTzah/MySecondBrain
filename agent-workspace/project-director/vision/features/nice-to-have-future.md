# Nice-to-Have Features — Feature Spec

## What the User Accomplishes
These are long-term vision features that are not required for the initial release. The data model and architecture should be designed to accommodate these without requiring rework.

## Priority
Nice-to-Have — can wait indefinitely. Not part of initial release scope.

## Detailed Behavior

### T1. Macro Genesis
- **Concept:** Compile successful chat sequences into permanent Tier 1 hotkeys
- **Example:** User repeatedly uses a 3-step workflow: "Summarize this article" → "Rewrite for clarity" → "Format as bullet points." Macro Genesis would compile this into a single hotkey that executes all three steps in sequence.
- **Architecture Requirement:** ChatThreads should support being "compiled" into reusable macros. Text Actions (K1) should support multi-step chaining.

### T2. Context-Aware Grouping
- **Concept:** Auto-tag threads by window context
- **Example:** All threads where source was VS Code are auto-tagged "vscode". All threads from Word are auto-tagged "writing".
- **Architecture Requirement:** ChatThread already stores SourceAppName (P3). Grouping/filtering by this field should be supported.

### T3. Passive Autonomous Threads
- **Concept:** Local vision watchdogs proactively spawn threads
- **Example:** App detects user has been reading about a topic and suggests "Want me to summarize what you've been learning about [topic]?"
- **Architecture Requirement:** Event-driven thread creation. Background monitoring hooks (file system watcher already exists for wiki N1; could be extended).

### T4. Screenshot/Screen Awareness
- **Concept:** Include screenshot of active window in Tier 1/Tier 2 actions for visual context
- **Example:** User takes screenshot of a UI mockup and asks "What do you think of this layout?" — AI receives the image for visual analysis.
- **Architecture Requirement:** Screen capture capability. Integration with multi-modal vision models. Media Library (G) already handles image storage.

### T5. Video Generation
- **Concept:** AI generates video clips when future multi-modal models support it
- **Example:** "Create a 10-second clip showing how to use this function"
- **Architecture Requirement:** Video generation API integration. Media Library (G) already handles video storage and playback. Extends G5 (Audio Generation) pattern to video.

## Data Architecture Notes
- T1: Needs macro definitions referencing Text Action chains
- T2: SourceAppName already captured (P3); needs auto-tagging rules
- T3: Needs event triggers and autonomous thread creation
- T4: Screenshot capture + multi-modal model support
- T5: Video generation API + Media Library extension

## Interactions
- T1 extends K1 (Text Actions) and K3 (Tier 1 hotkeys)
- T2 extends L7 (tags) and P3 (spatial anchoring)
- T3 extends O1 (ChatThread creation)
- T4 extends K3/K4 (Tier 1/2) and G (Media Library)
- T5 extends G (Media Library) and C9 (drag-drop media)
