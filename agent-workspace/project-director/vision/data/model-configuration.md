# ModelConfiguration — Data Entity

## Description
A Model Configuration defines the ENGINE — what model runs, on which provider, with what parameters. This is the "hardware" layer. Personas (behavior) reference Model Configurations (engine).

## Attributes

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| id | string (UUID) | Yes | Unique | Primary identifier |
| displayName | string | Yes | Max 100 chars. Unique | Display name (e.g., "GPT-4o — Fast") |
| provider | string | Yes | Must match a configured API key provider | Provider name |
| apiKeyId | string (FK) | Yes | References ApiKey | API key to use |
| modelIdentifier | string | Yes | e.g., "gpt-4o", "claude-sonnet-4-20250514" | Model ID string for API calls |
| temperature | number | Yes | Range: 0.0–2.0, step 0.1. Default: 1.0 | Randomness/creativity control |
| maxOutputTokens | integer | Yes | Min: 1. Model-dependent max | Maximum tokens in response |
| maxContextWindow | integer | Yes | Model-dependent | Model's maximum context window size |
| thinkingEnabled | boolean | Yes | Default: false | Whether extended thinking is enabled |
| pricingInputPer1K | number | No | Cost per 1000 input tokens (USD) | For cost estimation |
| pricingOutputPer1K | number | No | Cost per 1000 output tokens (USD) | For cost estimation |
| contextOverflowStrategy | enum | Yes | SlidingWindow, HardStop, AutoSummarize | B8 strategy |
| createdAt | datetime | Yes | Auto-set | Creation timestamp |
| updatedAt | datetime | Yes | Auto-updated | Last modification timestamp |

## Lifecycle

### Create
- Settings → Model Configurations → "New Model Configuration." Fill form.
- Pricing fields optional — if left empty, cost estimation unavailable for this config.
- Context overflow strategy defaults to SlidingWindow.

### Update
- Edit any field. Changes take effect for subsequent messages.
- Strategy changeable mid-chat from toolbar (B8).
- **Duplicate:** Creates copy with "(Copy)" suffix. Quick way to create variations.

### Delete
- If referenced by any Persona, show warning: "This Model Configuration is used by [N] Personas. Deleting it will require those Personas to select a new configuration."
- ⚠️ FLAGGED: FK handling on delete — nullify or cascade restrict? Architect decision.

## Relationships
- **references** ApiKey (via apiKeyId)
- **has many** Personas (Personas reference this as defaultModelConfigId)
- **has many** Messages (messages generated with this config)

## UI Visibility
- [`screens/settings.md`](screens/settings.md) — Model Configurations section
- Persona form (B3) — Dropdown to select default Model Configuration
- Text Actions form (K1) — Dropdown to select Model Configuration for action
- Chat header (C11) — Context window display references maxContextWindow
