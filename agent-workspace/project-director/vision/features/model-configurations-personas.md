# Model Configurations & Personas — Feature Spec

## What the User Accomplishes
The user sets up and manages AI providers, model configurations, and personas using a two-layer architecture: Model Configurations define the engine (what model runs), Personas define the behavior (system prompt, preferred model).

## Two-Layer Architecture
- **Model Configurations:** Hardware/engine parameters — provider, model ID, temperature, tokens, pricing
- **Personas:** Behavioral parameters — system prompt, default Model Configuration, default chat mode
- Personas reference Model Configurations; Model Configurations reference API Keys

## Trigger
Settings → Profiles section. Persona selector in textbox toolbar (B4). New chat creation (Ctrl+N).

## Detailed Behavior

### B1. API Key Management
- **Add Key:** Select provider type → enter key → optional display name → "Test Key" validates → "Save"
- **Provider Types:** OpenAI, Anthropic (Claude), Google, DeepSeek, Xiaomi MiMo, Moonshot, Mistral, "OpenAI-Compatible"
- **Encryption:** Keys encrypted at rest via Windows DPAPI (tied to local Windows user account)
- **Test Key:** Sends a minimal API request. Shows green checkmark (success) or red error with details (failure)
- **Edit:** Change key value or display name
- **Delete:** Confirmation dialog: "Delete this API key? Any Model Configurations using it will need a new key."
- **Display:** Keys shown masked (e.g., "sk-...abc123") with copy button

### B2. Model Configurations (The Engine)
- **Create:** "New Model Configuration" → fill form:
  - Display Name (required, e.g., "GPT-4o — Fast")
  - Provider (dropdown from B1)
  - API Key (dropdown: "Use Global Key" or specific key)
  - Model Identifier (dropdown from B7 or manual entry)
  - Temperature (slider 0.0–2.0, step 0.1, default 1.0)
  - Max Output Tokens (numeric, default: model's default)
  - Max Context Window (numeric, default: model's default)
  - Thinking On/Off (toggle, for models that support it)
  - Pricing: cost per 1K input tokens, cost per 1K output tokens (for cost estimation in C11)
  - Context Overflow Strategy (B8): Sliding Window / Hard Stop / Auto-Summarize
- **Edit/Delete:** Modify or remove configurations. Deleting a config that's referenced by a Persona shows warning.
- **Duplicate:** Quick way to create variations (e.g., "GPT-4o — Fast" → "GPT-4o — Creative" with higher temperature)

### B3. Personas (The Behavior)
- **Create:** "New Persona" → fill form:
  - Display Name (required, e.g., "Python Expert", "Writing Coach")
  - System Prompt (required, multi-line text area, supports {{variables}})
  - Default Model Configuration (dropdown from B2, required)
  - Default Chat Mode (radio: Standard or Text Completion)
- **Edit/Delete:** Modify or remove. Deleting a Persona that's the default (A2) prompts to select new default.
- **Built-in Defaults:** App ships with 2-3 starter Personas (e.g., "General Assistant", "Code Helper")
- **System Prompt Variables:** {{date}}, {{time}}, {{user_name}} resolved at message send time

### B4. Persona Selection per Chat
- **New Chat (Ctrl+N):** Opens Persona picker dialog or dropdown. Selecting a Persona creates chat with that Persona's system prompt, Model Configuration, and mode.
- **In-Chat Switching:** Persona dropdown in textbox toolbar. Changing Persona updates system prompt and Model Configuration for all SUBSEQUENT messages. Past messages retain their original Persona labels.
- **Recently Used:** Top of dropdown shows 5 most recently used Personas
- **Persona Indicator:** Chat header shows active Persona name and icon

### B5. Local Open-Source Model Support
- Uses "OpenAI-Compatible" provider type (B6) pointing to localhost endpoint
- No special UI — treated identically to any other provider
- Common local servers: LM Studio (localhost:1234), Ollama (localhost:11434), text-generation-webui

### B6. "OpenAI-Compatible" Provider Type
- **Provider Display Name:** User-chosen (e.g., "My Local Llama")
- **API Endpoint URL:** Full URL including path (e.g., http://localhost:1234/v1)
- **API Key:** Optional (many local servers don't require one)
- When saved, appears in provider dropdowns alongside built-in types

### B7. Auto-Fetch Available Models
- **Trigger:** After saving a new API key for a known provider (OpenAI, Anthropic, etc.)
- **Fetch:** Calls provider's /models endpoint. Shows spinner during fetch.
- **Cache:** Results cached locally. Manual "Refresh Models" button available.
- **Fallback:** If fetch fails, user can manually type model identifier
- **For OpenAI-Compatible:** No auto-fetch — user always enters manually

### B8. Context Window Overflow Strategy
Three strategies, configurable per Model Configuration:

1. **Sliding Window (Default):** When approaching max context, silently drops oldest messages (behind the system prompt). Newest messages always preserved. No user notification.

2. **Hard Stop:** When token count reaches 90% of max context, "Send" button grays out. Tooltip: "Context window nearly full (X/Y tokens). Reduce message size or clear conversation." User must manually truncate.

3. **Auto-Summarize:** When reaching 80% of max context, an AI agent silently summarizes the oldest 50% of the conversation into a single summary block inserted after the system prompt. Original messages replaced. Summary block is visible and labeled: "[Earlier conversation summarized]". User can expand to see what was summarized.

- Strategy changeable mid-chat from textbox toolbar
- ⚠️ FLAGGED: Auto-Summarize requires a separate API call that costs tokens. This should be transparent to the user.

## Data
- [`data/model-configuration.md`](data/model-configuration.md), [`data/persona.md`](data/persona.md), [`data/api-key.md`](data/api-key.md)

## Success/Failure States
- **Success — Key Tested:** Green: "API key validated successfully. [N] models available."
- **Failure — Key Invalid:** Red: "API key validation failed. (401) Invalid API key."
- **Failure — Network Error:** Red: "Could not reach [provider]. Check your internet connection."
- **Failure — Fetch Models Failed:** Yellow warning: "Could not fetch model list. You can enter model identifiers manually."

## Permissions
- Single-user app. All configurations managed by the sole user.

## Interactions
- B1 → B2 (configs reference keys)
- B2 → B3 (personas reference configs)
- B3 → B4 (personas selected for chats)
- B8 → C11 (context window display), E5 (system message editing)
