# Language & RTL Support — Feature Spec

## What the User Accomplishes
The app supports English (left-to-right) as the default UI language and automatically handles Hebrew (right-to-left) text in messages based on Unicode character ranges. Mixed LTR/RTL messages render each segment in its correct direction.

## Trigger
- Automatic detection based on Unicode character ranges in message content
- No manual language switching needed

## Detailed Behavior

### Q1. English (LTR)
- Default language for all UI elements
- All labels, buttons, menus, settings, tooltips in English
- Text rendering direction: left-to-right
- No localization to other languages planned

### Q2. Hebrew (RTL)
- Messages containing Hebrew characters (Unicode range U+0590–U+05FF) automatically render right-to-left
- Detection is per-message, based on character content
- RTL messages align to the right edge of the message container
- Punctuation at end of RTL text appears on the left side

### Q3. Mixed LTR/RTL Messages
- Messages containing both English and Hebrew characters render each segment in its correct direction
- Uses Unicode Bidirectional Algorithm (UBA) for segment-level direction
- Example: A message with Hebrew text and an English code snippet should show Hebrew right-aligned, code snippet left-aligned
- **Text Input:** The textbox input respects typing direction. Cursor position and text selection follow the active text direction.
- ⚠️ FLAGGED: Rich text input with bidirectional text is complex. The Architect should evaluate WPF's built-in BiDi support vs custom rendering.

## Data
- No additional data entities. Language detection is runtime behavior.

## Success/Failure States
- **RTL Detection:** Automatic. No user-facing success/failure states.
- **Mixed Text Rendering:** If bidirectional rendering fails, text may appear misaligned. This is a rendering bug, not a user error.

## Permissions
- Single-user app.

## Interactions
- Q2/Q3 affects C1 (conversation view), C2 (message rendering), C4 (streaming display), textbox input
