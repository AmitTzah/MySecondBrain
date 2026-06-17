# Global Artifacts Browser — Screen Specification

## Purpose

Lists all AI-generated artifacts (code files, documents, config files) from ALL chats in a single searchable, filterable view. Clicking an artifact opens it in the Studio side panel for viewing with syntax highlighting and version history.

## Layout

Standard Studio frame with sidebar. Content area:
- Search bar + filter controls (top)
- Artifacts table/list (main area)

## Regions

### Region 1: Studio Sidebar
📄 Artifacts nav item active.

### Region 2: Content Area

**Search & Filters:**
- Search bar: searches by artifact name
- Filter dropdowns: Type (All, Python, JavaScript, Markdown, JSON, YAML, Other), Source Chat, Date Range
- Sort: Newest (default), Name A-Z, Type

**Artifacts Table:**
- Columns: Name, Type (icon + label), Source Chat (clickable → opens chat), Created, Last Modified, Versions count
- Click artifact name: opens in Studio side panel (F6) with syntax highlighting, version dropdown, Save to Disk/Save to Wiki
- Empty: "No artifacts yet. Artifacts are created when AI generates code, documents, or config files during conversations."

## Navigation

**Entry:** Studio sidebar → 📄 Artifacts, or Studio side panel → "View All Artifacts"
**Exit:** Studio sidebar → any nav item

## Cross-References

- Feature spec: [`features/artifacts-side-panel.md`](../features/artifacts-side-panel.md) F7
