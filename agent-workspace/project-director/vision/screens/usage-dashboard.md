# Usage Dashboard — Screen Specification

## Purpose

The Usage Dashboard provides comprehensive usage statistics — token counts and estimated costs across all providers, models, and chats. Filterable by time range. Charts visualize trends. Per-chat breakdown table. AI feedback summary with approval percentages.

## Layout

Standard Studio frame with sidebar. Scrollable single-column content:
- Summary cards (top row)
- Time range filter
- Charts (line, bar, pie)
- Per-chat breakdown table
- AI Feedback Summary section

## Regions

### Region 1: Studio Sidebar
📊 Usage nav item active. All other nav items link to their screens.

### Region 2: Content Area

**Summary Cards (top row, 4 cards):**
- Total Tokens (this month)
- Total Cost (this month)
- Most Used Model
- Most Used Persona

**Time Range Filter:**
- Preset buttons: Today | This Week | This Month (default, active) | Custom Range | All Time
- Custom Range shows date picker

**Charts (two-column grid):**
- Line Chart: Token usage over time (input + output lines)
- Bar Chart: Cost over time
- Pie Chart: By Provider
- Pie Chart: By Model
- ⚠️ Charts use placeholder boxes in HTML mock ("📊 Chart would render here")

**Per-Chat Breakdown Table:**
- Columns: Chat Title, Date, Models Used, Tokens (In + Out), Cost, Persona
- Sortable by clicking column headers
- Click chat title → opens that chat in Studio
- Empty: "No chat activity in the selected time range."

**AI Feedback Summary (S6):**
- Table: Persona | Total Rated | 👍 | 👎 | Approval %
- Top 5 Most 👍 Personas / Most 👎 Personas
- Approval trend chart (placeholder)
- Empty: "No feedback recorded yet."

## Navigation

**Entry:** Studio sidebar → 📊 Usage
**Exit:** Studio sidebar → any nav item

## Cross-References

- Feature spec: [`features/usage-pricing-dashboard.md`](../features/usage-pricing-dashboard.md) S1-S6
