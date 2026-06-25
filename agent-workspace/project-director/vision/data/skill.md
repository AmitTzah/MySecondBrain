# Skill — Data Entity

## Description

Metadata about an installed Agent Skill. Skills are Markdown instruction files (`SKILL.md`) that encode domain-specific procedural knowledge. Discovered at startup from embedded resources and filesystem paths. Not persisted to SQLite — re-discovered each launch. This entity describes the in-memory representation used during the app session.

## Attributes

| Attribute | Type | Required | Constraints |
|-----------|------|----------|-------------|
| `name` | string | Required | Kebab-case identifier (e.g., "xlsx", "web-artifacts-builder"). Must be unique within a source scope. |
| `description` | string | Required | Extracted from SKILL.md YAML frontmatter. Must be non-empty for skill to be usable (required for disclosure in system prompt catalog). |
| `source` | enum | Required | `built-in` (embedded in app DLL), `user` (%LOCALAPPDATA%/skills/), `cross-client` (~/.agents/skills/ or ~/.claude/skills/) |
| `location` | string | Required | Path to the SKILL.md file (filesystem path or embedded resource identifier) |
| `enabled` | boolean | Required | Whether the skill is enabled globally (Settings → Skills) or per-chat (toolbar override) |
| `dependencies` | object | Optional | Package dependencies (Python packages, Node.js packages), system tool requirements (LibreOffice, pandoc). Populated from skill content analysis or explicit dependency metadata. |

## Lifecycle

### Discovery (App Startup)

- **Built-in:** Loaded from embedded resources in `MySecondBrain.UI.dll` → `Skills/anthropic/`. Always present, cannot be deleted by user.
- **User:** Scanned from `%LOCALAPPDATA%/MySecondBrain/skills/`. User adds by copying skill directories. Survives app updates.
- **Cross-client:** Scanned from `%USERPROFILE%/.agents/skills/` and `%USERPROFILE%/.claude/skills/`. From other compliant tools.
- **Name collisions:** User overrides built-in. Cross-client overrides user. First-found wins within same scope.

### Enable/Disable

- **Global defaults:** Settings → Skills (A12). Toggle per skill. New chats inherit.
- **Per-chat:** Textbox toolbar "📚 Skills ▼" dropdown. Temporary override for current chat.
- **Disabled:** Removed from system prompt catalog and `skill_load` tool enum entirely.

### Updates

- **Built-in:** Updated with app updates (new `MySecondBrain.UI.dll`).
- **User/Community:** Never overwritten by app updates. User manually updates by replacing skill directories.

### Removal

- **Built-in:** Cannot be removed. Can be disabled.
- **User/Community:** Delete directory from `%LOCALAPPDATA%/MySecondBrain/skills/`. Re-discovered on next app launch.

## Relationships

| Relationship | Entity | Type |
|-------------|--------|------|
| — | — | Skills have no database relationships. They are filesystem-based instruction documents. Skill activation state is tracked in-memory per session (not persisted). |

## UI Visibility

| Screen | How It Appears |
|--------|---------------|
| **Settings → Skills** (A12) | List of all discovered skills: name, description, source badge, enable/disable toggle, dependency status indicators. "Enable All" / "Disable All" quick actions. Community skills show "source: community" annotation. |
| **Studio Chat Toolbar** | "📚 Skills ▼" dropdown with individual checkboxes. "All on/off" at top. |
| **System Prompt** | `<available_skills>` XML block with name + description for each enabled skill. Only present if ≥1 skill enabled. |
| **Chat (tool calls)** | `skill_load("xlsx")` appears as system message: "📚 Loaded skill: xlsx." Result shown as collapsible skill content block. |

## Cross-References

- Defined by: [`features/agent-skills.md`](../features/agent-skills.md) §W1-W5
- Enabled in: [`features/settings-configuration.md`](../features/settings-configuration.md) §A12
- Activated by: [`features/tool-use-agents.md`](../features/tool-use-agents.md) §H12 (skill_load tool)
- Listed in: System prompt catalog (W2, W7)
- Not persisted: Skills are re-discovered from filesystem on each app launch. This entity is in-memory only during the app session.
