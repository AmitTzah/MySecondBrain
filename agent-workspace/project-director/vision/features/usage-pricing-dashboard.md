# Usage & Pricing Dashboard — Feature Spec

## What the User Accomplishes

The user views comprehensive usage statistics — token counts (including cache tokens), latency, and estimated costs — across all providers, models, tiers, and chats. Every single API call is logged regardless of tier. Powerful interactive filtering by provider, model, tier, and time range. Charts visualize usage trends, cache efficiency, and latency distribution. Budget alerts prevent overspending. AI response feedback summary with approval percentages.

Reference UI aesthetic: DeepSeek's usage dashboard design.

## Trigger

- Studio sidebar → "Usage" nav item (📊)
- Settings → "Pricing" category (for budget settings)

## Detailed Behavior

### S1. Usage Overview Screen

- **Access:** Dedicated screen accessible from Studio sidebar
- **Filter Bar (top, always visible, applies to all sections below):**
  - **Provider dropdown:** Multi-select with checkboxes. "All" / individual providers. Selecting/deselecting providers filters the Model dropdown.
  - **Model dropdown:** Multi-select with checkboxes. "All" / individual models. Filtered by selected providers.
  - **Tier checkboxes:** ☑ Tier 1 ☑ Tier 2 ☑ Tier 3 — all checked by default.
  - **Time Range:** Preset buttons: Today | This Week | This Month (default) | Custom Range | All Time. Custom Range shows date picker.
- All charts, tables, and summary cards update to reflect the active filters.

### S2. Summary Cards

Row of summary cards at the top:

| Card | Content |
|------|---------|
| **Total Tokens** | Sum of totalTokens (prompt + completion). With delta vs previous period. |
| **Total Cost** | Sum of estimatedCost in USD. With delta vs previous period. |
| **Cache Hit Rate** | cacheReadTokens / (cacheReadTokens + promptTokens) as percentage. Indicates how effectively caching reduces costs. |
| **Avg Latency** | Average latencyMs across all calls. |
| **Most Used Model** | Model with highest totalTokens. |
| **Total API Calls** | Count of UsageRecords. Including errors. |

### S3. Usage Charts

Two-column grid of charts:

**Tokens Over Time (Line Chart):**
- X-axis: dates (day or week depending on time range)
- Y-axis: token count
- Lines: Prompt tokens, Completion tokens, Cache Read tokens (stacked or separate, toggleable)
- Hover tooltip shows exact values per data point

**Cost Over Time (Bar Chart):**
- X-axis: dates
- Y-axis: cost in USD
- Stacked bars by provider (each provider a different color segment)
- Toggle: stacked by provider / stacked by model

**Cache Breakdown (Stacked Bar or Pie):**
- Cache Read Tokens vs Cache Creation Tokens vs Non-Cached Prompt Tokens
- Per provider breakdown (toggle)
- Shows cache efficiency — high cache read ratio = good

**Latency Distribution (Table + Chart):**
- Table per model: Avg, p50 (median), p95, p99 latency in milliseconds
- Bar chart: each model as a bar, with p50/p95/p99 markers
- Sorted by avg latency (fastest first)

### S4. Per-Provider Breakdown Table

| Column | Description |
|--------|-------------|
| Provider | Provider name |
| Calls | Number of API calls |
| Total Tokens | Sum of totalTokens |
| Cache Hit Rate | cacheReadTokens / (cacheReadTokens + promptTokens) as % |
| Avg Latency | Average latencyMs |
| Total Cost | Sum of estimatedCost |

Sortable by clicking column headers. Click provider row → filters model table below to that provider.

### S5. Per-Model Breakdown Table

| Column | Description |
|--------|-------------|
| Model | Model identifier |
| Provider | Provider name |
| Calls | Number of API calls |
| Total Tokens | Sum of totalTokens |
| Cache Hit Rate | cacheReadTokens / (cacheReadTokens + promptTokens) as % |
| Avg Latency | Average latencyMs |
| p95 Latency | 95th percentile latencyMs |
| Total Cost | Sum of estimatedCost |

Sortable by clicking column headers. Click model row → filters per-chat table below to that model.

### S6. Per-Chat Breakdown Table

| Column | Description |
|--------|-------------|
| Chat Title | Chat name (clickable — opens chat in Studio) |
| Date | Date of last activity |
| Tier | 1, 2, or 3 badge |
| Model(s) Used | Model identifiers (may be multiple) |
| Tokens (In + Out) | promptTokens + completionTokens |
| Cache Tokens | cacheReadTokens + cacheCreationTokens |
| Avg Latency | Average latencyMs for this chat |
| Cost | Sum of estimatedCost |

Sortable. Click chat title → opens that chat in Studio.

### S7. Budget Alerts

- **Monthly Budget:** User sets a monthly spending limit in Settings → Pricing (in USD)
- **Warning Threshold:** Configurable percentage (default: 80%). When reached: non-intrusive warning in Studio
- **Block Threshold:** Optional. When 100% reached: block further API calls
- **Reset:** Budget resets on 1st of each month

### S8. AI Response Feedback Summary

- **Feedback Per Persona:** Table: Persona, Total Rated, 👍, 👎, Approval %
- **Feedback Per Model:** Same breakdown by Model Configuration
- **Feedback Trend:** Line chart — approval % over time (by week/month)
- **Rankings:** "Most 👍 Personas" and "Most 👎 Personas" top-5 lists
- **Filter:** Time range filter applies to feedback data

## Data

- [`data/usage-record.md`](data/usage-record.md) — enriched UsageRecord with cache tokens, latency, tier, error info, raw JSON path
- Every single API call across all three tiers is logged from the very first call

## Success/Failure States

- **No Data:** "No usage data yet. Start chatting to see your usage statistics."
- **Pricing Not Configured:** Cost charts show "Cost data unavailable — configure pricing in Model Configurations (B2)."
- **No Cache Data:** Cache charts show "Cache data not available — your provider may not support prompt caching, or no cacheable requests have been made."
- **Budget Warning:** Non-intrusive banner: "⚠️ Budget alert: [X]% of monthly limit used ($[Y] of $[Z])."
- **Budget Exceeded (Block enabled):** API calls blocked. "Monthly budget exceeded. Increase limit in Settings → Pricing or wait until next month."
- **All Tiers Deselected:** "Select at least one tier to view usage data."

## Permissions

- Single-user app.

## Interactions

- S1-S6 read from UsageRecords generated by every ILLMProvider call
- S7 references B2 (model pricing configuration, including cache token pricing)
- S8 reads from Message.feedback field (D8)
- API History Viewer reads raw JSON log: [`features/api-history-viewer.md`](api-history-viewer.md)
- Per-chat breakdown navigates to chats via L1 (sidebar)
