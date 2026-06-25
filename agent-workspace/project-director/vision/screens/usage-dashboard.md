# Usage Dashboard — Screen Specification

## Purpose

The Usage Dashboard provides comprehensive usage statistics — token counts (including cache tokens), latency, and estimated costs — across all providers, models, tiers, and chats. Every single API call is logged regardless of tier. Interactive filtering by provider, model, tier, and time range. Charts visualize usage trends, cache efficiency, and latency distribution. Budget alerts and AI feedback summary.

Reference UI aesthetic: DeepSeek's usage dashboard design.

## Layout

Standard Studio frame with sidebar. Scrollable single-column content:
- Filter bar (top, always visible)
- Summary cards row
- Charts grid (tokens over time, cost over time, cache breakdown, latency distribution)
- Per-provider and per-model breakdown tables
- Per-chat breakdown table
- Budget alerts and AI feedback summary sections

## Regions

### Region 1: Studio Sidebar
📊 Usage nav item active. All other nav items link to their screens.

### Region 2: Content Area

**Filter Bar (top, always visible, applies to all sections below):**
- **Provider dropdown:** Multi-select with checkboxes. "All" / individual providers (Anthropic, OpenAI, Google, DeepSeek, etc.). Selecting/deselecting providers filters the Model dropdown.
- **Model dropdown:** Multi-select with checkboxes. "All" / individual models. Filtered by selected providers.
- **Tier checkboxes:** ☑ Tier 1 ☑ Tier 2 ☑ Tier 3 — all checked by default.
- **Time Range:** Preset buttons: Today | This Week | This Month (default, active) | Custom Range | All Time. Custom Range shows date picker.

**Summary Cards (top row, 6 cards):**
- Total Tokens (with delta vs previous period)
- Total Cost (with delta vs previous period)
- Cache Hit Rate (cacheReadTokens / total prompt tokens as %)
- Avg Latency (ms)
- Most Used Model
- Total API Calls (including errors)

**Charts (two-column grid):**

- **Line Chart — Tokens Over Time:** X-axis: dates, Y-axis: token count. Lines: Prompt tokens, Completion tokens, Cache Read tokens (stacked or separate, toggleable). Hover tooltip.

- **Bar Chart — Cost Over Time:** X-axis: dates, Y-axis: cost in USD. Stacked bars by provider. Toggle: by provider / by model.

- **Cache Breakdown:** Stacked bar or pie — Cache Read vs Cache Creation vs Non-Cached Prompt tokens. Per provider breakdown (toggle).

- **Latency Distribution:** Table per model: Avg, p50, p95, p99 latency (ms). Bar chart with p50/p95/p99 markers. Sorted by avg latency (fastest first).

- ⚠️ Charts use placeholder boxes in HTML mock ("📊 Chart would render here")

**Per-Provider Breakdown Table:**
- Columns: Provider, Calls, Total Tokens, Cache Hit Rate %, Avg Latency, Total Cost
- Sortable by clicking column headers
- Click provider row → filters model table below

**Per-Model Breakdown Table:**
- Columns: Model, Provider, Calls, Total Tokens, Cache Hit Rate %, Avg Latency, p95 Latency, Total Cost
- Sortable by clicking column headers
- Click model row → filters per-chat table below

**Per-Chat Breakdown Table:**
- Columns: Chat Title (clickable), Date, Tier badge, Model(s), Tokens (In + Out), Cache Tokens, Avg Latency, Cost
- Sortable by clicking column headers
- Click chat title → opens that chat in Studio
- Empty: "No chat activity in the selected time range."

**Budget Alerts (S7):**
- Monthly budget limit, warning threshold (default 80%), block on exceed toggle
- Configured in Settings → Pricing

**AI Feedback Summary (S8):**
- Table: Persona | Total Rated | 👍 | 👎 | Approval %
- Top 5 Most 👍 Personas / Most 👎 Personas
- Approval trend chart (placeholder)
- Empty: "No feedback recorded yet."

## Navigation

**Entry:** Studio sidebar → 📊 Usage
**Exit:** Studio sidebar → any nav item

## Cross-References

- Feature spec: [`features/usage-pricing-dashboard.md`](../features/usage-pricing-dashboard.md) S1-S8
- Data entity: [`data/usage-record.md`](../data/usage-record.md) — enriched with cache tokens, latency, tier, error info
- Every single API call across all 3 tiers logged from the very first call
