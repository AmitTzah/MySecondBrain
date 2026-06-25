# UsageRecord — Data Entity

## Description

A UsageRecord captures complete metadata for a single AI API call — token consumption (including cache tokens), latency, cost, provider/model identity, tier origin, error information (if failed), and a path to the raw JSON log for debugging. Records are aggregated to power the Usage & Pricing Dashboard (S) and the API History Viewer.

Every single API call across all three tiers (Tier 1 hotkey, Tier 2 command bar, Tier 3 Studio) is logged. This infrastructure MUST exist before Studio Chat (Feature 11) starts making real API calls.

## Attributes

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| id | string (UUID) | Yes | Unique | Primary identifier |
| messageId | string (FK) | No | References Message | The assistant message this usage is for. Null if the call failed before message creation. |
| threadId | string (FK) | Yes | References ChatThread | Parent chat thread (denormalized for querying) |
| personaId | string (FK) | No | References Persona | Persona used |
| modelConfigId | string (FK) | Yes | References ModelConfiguration | Model config used |
| provider | string | Yes | Provider name (e.g., "Anthropic", "OpenAI", "DeepSeek") | For provider-level aggregation |
| modelIdentifier | string | Yes | Model ID string (e.g., "claude-sonnet-4-20250514") | For model-level aggregation |
| promptTokens | integer | Yes | ≥ 0 | Tokens in the prompt (input) |
| completionTokens | integer | Yes | ≥ 0 | Tokens in the completion (output) |
| totalTokens | integer | Yes | promptTokens + completionTokens | Total tokens consumed |
| cacheReadTokens | integer | Yes | ≥ 0. Default: 0 | Cache read/hit tokens. Anthropic: `cache_read_input_tokens`. DeepSeek: `cache_hit_tokens`. Provider-agnostic field — zero if provider doesn't support caching. |
| cacheCreationTokens | integer | Yes | ≥ 0. Default: 0 | Cache creation/write tokens. Anthropic: `cache_creation_input_tokens`. DeepSeek: `cache_miss_tokens`. Provider-agnostic field — zero if provider doesn't support caching. |
| latencyMs | integer | Yes | ≥ 0 | Time from request sent to full response complete, in milliseconds |
| estimatedCost | number | No | In USD. Calculated from ModelConfiguration pricing × token counts | Estimated cost including cache token pricing (cache reads typically cost less than regular prompt tokens) |
| tier | integer | Yes | 1, 2, or 3 | Which interaction tier generated this API call: 1 = Tier 1 Hotkey, 2 = Tier 2 Command Bar, 3 = Tier 3 Studio |
| errorType | string | No | Null if successful | Error classification: "auth", "rate_limit", "network", "timeout", "server", "unknown" |
| errorMessage | string | No | Null if successful | Human-readable error message from the provider or app |
| errorStatusCode | integer | No | Null if successful | HTTP status code from the provider (401, 429, 500, etc.) |
| rawJsonPath | string | No | Path to the per-chat raw JSON log file | Filesystem path: `%LOCALAPPDATA%/MySecondBrain/workspace/{chat-id}/_api_history.json`. Used by the API History Viewer. |
| createdAt | datetime | Yes | Auto-set | When the API call completed (or failed) |

### Cache Token Fields — Provider Mapping

| Field | Anthropic | DeepSeek | OpenAI | Google | Other |
|-------|-----------|----------|--------|--------|-------|
| cacheReadTokens | `cache_read_input_tokens` | `cache_hit_tokens` | `prompt_tokens_details.cached_tokens` | TBD | 0 if not supported |
| cacheCreationTokens | `cache_creation_input_tokens` | `cache_miss_tokens` | N/A (OpenAI doesn't expose cache writes separately) | TBD | 0 if not supported |

The app normalizes provider-specific cache token fields into the two provider-agnostic fields. If a provider doesn't support caching or doesn't expose a particular cache metric, the field defaults to 0.

## Lifecycle

### Create
- Auto-created when EVERY AI API call completes (or fails). Token counts from API response headers/body.
- Cost calculated from ModelConfiguration pricing × token counts. Cache reads typically cost less than regular prompt tokens — pricing per ModelConfiguration should include cache token rates.
- `rawJsonPath` set to the per-chat JSON log file. The app appends the full request/response JSON to this file on every call.
- If the API call fails (network error, auth error, rate limit), a UsageRecord is STILL created with `errorType`, `errorMessage`, `errorStatusCode` set. Token fields will be 0.

### Update
- Immutable after creation. Usage records are append-only.
- The `rawJsonPath` file is append-only — new calls append to the array, existing entries are never modified.

### Delete
- Cascading: when parent ChatThread deleted (L4/O4), usage records and the raw JSON log file are deleted.
- Not user-deletable individually.

## Relationships
- **belongs to** Message (via messageId) — nullable (null if call failed before message creation)
- **belongs to** ChatThread (via threadId)
- **references** Persona (via personaId)
- **references** ModelConfiguration (via modelConfigId)

## UI Visibility
- **Usage Dashboard (S1-S6):** Aggregated into charts and tables — tokens over time, cost over time, cache breakdown, latency distribution, per-provider/model tables, per-chat breakdown, budget alerts
- **Per-message display (C11):** Individual message token count + cost + latency
- **Chat header (C11):** Cumulative chat cost
- **API History Viewer:** Reads the per-chat raw JSON log file (`rawJsonPath`) for debugging — see [`features/api-history-viewer.md`](../features/api-history-viewer.md)
