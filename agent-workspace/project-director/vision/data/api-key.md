# ApiKey — Data Entity

## Description
An API key is a credential for accessing an AI provider's API. Keys are encrypted at rest using Windows DPAPI. The user manages their own keys — the app never creates provider accounts.

## Attributes

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| id | string (UUID) | Yes | Unique | Primary identifier |
| displayName | string | No | Max 100 chars | User-friendly label (e.g., "My OpenAI Key") |
| provider | string | Yes | One of: OpenAI, Anthropic, Google, DeepSeek, MiMo, Moonshot, Mistral, OpenAICompatible | Provider type |
| customProviderName | string | Conditional | Required if provider=OpenAICompatible | User-chosen provider display name |
| customEndpointUrl | string | Conditional | Required if provider=OpenAICompatible | Full API endpoint URL |
| keyValue | string | Yes | Encrypted via DPAPI at rest. Never displayed in full after save. Must be redacted (`[REDACTED]`) in all diagnostic log output via Serilog destructuring policy (V1). | The API key secret |
| createdAt | datetime | Yes | Auto-set | Creation timestamp |
| lastTestedAt | datetime | No | Updated when "Test Key" is used | Last successful validation |
| isValid | boolean | No | Set by Test Key validation | Whether key is currently valid |

## Lifecycle

### Create
- Settings → Providers → "Add API Key." Select provider type → enter key → optional name → "Test Key" validates → "Save."
- Key encrypted via Windows DPAPI before storage.
- For OpenAICompatible: also enter custom provider name and endpoint URL.

### Update
- Edit display name or key value.
- Changing key value triggers re-encryption.
- "Test Key" re-validates.

### Delete
- If referenced by Model Configurations, warn: "This key is used by [N] Model Configurations. Deleting it will break those configurations."
- Key permanently deleted from encrypted storage.

## Relationships
- **has many** ModelConfigurations (configs reference this key)

## UI Visibility
- [`screens/settings.md`](screens/settings.md) — Providers section. List of keys with masked display.
- Model Configuration form (B2) — Dropdown to select API key.
- Onboarding Wizard (A8) — First key added during setup.
