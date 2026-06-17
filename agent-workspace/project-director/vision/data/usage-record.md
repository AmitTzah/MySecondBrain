# UsageRecord — Data Entity

## Description
A UsageRecord captures token consumption and estimated cost for a single AI API call. Records are aggregated to power the Usage & Pricing Dashboard (S).

## Attributes

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| id | string (UUID) | Yes | Unique | Primary identifier |
| messageId | string (FK) | Yes | References Message | The assistant message this usage is for |
| threadId | string (FK) | Yes | References ChatThread | Parent chat thread (denormalized for querying) |
| personaId | string (FK) | No | References Persona | Persona used |
| modelConfigId | string (FK) | Yes | References ModelConfiguration | Model config used |
| provider | string | Yes | Provider name | For provider-level aggregation |
| modelIdentifier | string | Yes | Model ID string | For model-level aggregation |
| promptTokens | integer | Yes | ≥ 0 | Tokens in the prompt |
| completionTokens | integer | Yes | ≥ 0 | Tokens in the completion |
| totalTokens | integer | Yes | promptTokens + completionTokens | Total tokens |
| estimatedCost | number | No | In USD. Calculated from pricing config | Estimated cost |
| createdAt | datetime | Yes | Auto-set | When the API call completed |

## Lifecycle

### Create
- Auto-created when an AI API call completes. Token counts from API response headers or local tokenizer (C11).
- Cost calculated from ModelConfiguration pricing × token counts.

### Update
- Immutable after creation. Usage records are append-only.

### Delete
- Cascading: when parent ChatThread deleted (L4/O4), usage records deleted.
- Not user-deletable individually.

## Relationships
- **belongs to** Message (via messageId)
- **belongs to** ChatThread (via threadId)
- **references** Persona (via personaId)
- **references** ModelConfiguration (via modelConfigId)

## UI Visibility
- Usage Dashboard (S1-S4) — Aggregated into charts and tables
- Per-message display (C11) — Individual message token count + cost
- Chat header (C11) — Cumulative chat cost
