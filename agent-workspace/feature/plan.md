# Feature Implementation Plan: Model Configurations, API Keys & Personas

## 1. Overall Project Context
MySecondBrain is a native Windows desktop application — a unified, provider-agnostic AI chat hub that replaces all LLM chat platforms, paired with a personal wiki for turning conversations into lasting knowledge. Built with WPF .NET 8, MVVM (CommunityToolkit.Mvvm), EF Core + SQLite, Microsoft.Extensions.DependencyInjection. Windows 10/11 only. The project follows a 7-project layered architecture (Core → Data → Services → UI) with compile-time dependency direction enforced via `<ProjectReference>`. All LLM providers follow the Provider/Adapter pattern: interface in Core, implementation in Services, registered via DI's `IEnumerable<T>` multi-implementation injection. The existing codebase has 76+ DI registrations, 14 entities, 8 repositories, a fully working WPF shell with 8 screen shells + navigation + theming, Serilog logging, and platform services (system tray, global hotkeys, WebSocket server, auto-update).

## 2. Feature-Specific Context
Feature 7 — Model Configurations, API Keys & Personas — is part of Wave 3 (Vertical Slices). It establishes the two-layer configuration architecture that powers all AI interactions: **Model Configurations** define "the engine" (provider, model ID, temperature, tokens, pricing) and **Personas** define "the behavior" (system prompt, default model config, chat mode). Personas reference Model Configurations; Model Configurations reference API Keys. This feature delivers 8 behavioral components: (B1) API Key Management with DPAPI encryption and 8 provider types, (B2) Model Configuration CRUD with temperature slider and context overflow strategy, (B3) Persona CRUD with {{variables}} and built-in defaults, (B4) Persona selection per chat with recently-used ordering, (B5) local open-source model support via OpenAI-Compatible, (B6) custom OpenAI-Compatible provider type, (B7) auto-fetch available models from provider APIs, and (B8) context window overflow strategy. The feature touches Settings → Providers, Settings → Profiles, and Studio Chat → Persona selector.

Key existing code: `ApiKey`, `ModelConfiguration`, and `Persona` entities exist with proper navigation properties. `ApiKeyRepository` has full CRUD with DPAPI patterns. `ModelConfigurationRepository` and `PersonaRepository` have basic CRUD with `Restrict` delete checks. All three domain models in `Core/Models/DomainModels.cs` exist but are incomplete (missing several fields from entity specs). `DpapiEncryptionService` is a stub returning empty arrays. All 4 LLM providers (`OpenAIProvider`, `AnthropicProvider`, `GoogleProvider`, `OpenAICompatibleProvider`) are stubs — `ValidateKeyAsync` and `ListModelsAsync` return empty/false results. `SettingsViewModel` only injects `ISettingsRepository`, `IThemeProvider`, `ILogger<T>`. `SettingsView.xaml` has a sidebar with 16 category items but a placeholder content area. `ChatView.xaml` has a hardcoded "Default Persona" header and "🤖 Default Persona ▾" toolbar button.

## 3. Architecture and Extensibility
This feature follows the existing **Provider/Adapter** pattern for LLM providers. Key architectural decisions:

**Two-Layer Configuration Architecture:** Model Configurations are the "engine" layer (provider, model, temperature, tokens, pricing). Personas are the "behavior" layer (system prompt, default model config, chat mode). This separation allows users to mix and match — the same persona can use different model configs, and the same model config can power different personas.

**IEncryptionService** (`Core/Interfaces/IEncryptionService.cs`) — abstraction for DPAPI encryption. `DpapiEncryptionService` implements it using `System.Security.Cryptography.ProtectedData` (Windows Data Protection API). The interface exposes `Protect(byte[])`/`Unprotect(byte[])` and `ProtectString(string)`/`UnprotectString(string)`. API keys are encrypted before storage and decrypted only when needed for API calls. Encryption is tied to the local Windows user account via DPAPI's `CurrentUser` scope.

**IApiKeyRepository** — CRUD for API keys. `ApiKeyRepository` handles the full lifecycle: create with encrypted value, update with re-encryption on key change, delete with nullification of ModelConfiguration FK references. The `DeleteAsync` method eagerly loads `ModelConfigurations` and sets their `ApiKeyId` to null before removing the key (SetNull FK behavior).

**IModelConfigurationRepository** — CRUD for model configs. Delete is restricted when config is referenced by any Persona (throws `InvalidOperationException` with descriptive message). This is handled at the application layer per the Restrict FK behavior.

**IPersonaRepository** — CRUD for personas. Includes `GetDefaultAsync()` which returns the first built-in persona (or first available). Built-in personas are seeded via `HasData()` with fixed GUIDs.

**LLM Provider Extensibility:** Adding a new provider actor requires: (a) add enum value to `ProviderType`, (b) create adapter class in `Services/LLM/` implementing `ILLMProvider`, (c) register via `services.AddSingleton<ILLMProvider, NewProvider>()`. The `LLMProviderFactory` auto-discovers it via `IEnumerable<ILLMProvider>`. The `ILLMProvider` interface exposes `ValidateKeyAsync(string apiKey, CancellationToken)` for B1 key testing and `ListModelsAsync(CancellationToken)` for B7 auto-fetch.

**OpenAI-Compatible Provider (B6):** Treated as a standard provider type but with additional fields: `CustomProviderName` and `CustomEndpointUrl` on the `ApiKey` entity. When `Provider = OpenAICompatible`, the endpoint URL is used instead of the default API base URL. No auto-fetch — user manually enters model identifiers.

**Settings UI Navigation:** SettingsView uses a two-column layout (category sidebar + content area). This feature fills the "Providers" and "Profiles" sidebar items. The content area switches via a bound `SelectedSettingsCategory` enum property, not via screen-level navigation — it stays within the Settings screen.

## 4. Final Expected Project Structure
*Below is what the project will look like once all implementation steps are complete.*
- `src/`
  - `MySecondBrain.Core/`
    - `Interfaces/`
      - `IEncryptionService.cs` — (existing, unchanged interface)
      - `IApiKeyRepository.cs` — (existing)
      - `IModelConfigurationRepository.cs` — (existing)
      - `IPersonaRepository.cs` — (existing)
      - `ILLMProvider.cs` — (existing, unchanged interface)
      - `ILLMProviderService.cs` — (existing, unchanged interface)
    - `Models/`
      - `DomainModels.cs` — **UPDATED**: ApiKey gains CustomProviderName, CustomEndpointUrl, CreatedAt, LastTestedAt, IsValid; ModelConfiguration gains ApiKeyId, MaxContextWindow, PricingInputPer1K, PricingOutputPer1K, ContextOverflowStrategy, CreatedAt, UpdatedAt; Persona gains DefaultModelConfigId, DefaultChatMode, CreatedAt, UpdatedAt
      - `Enums.cs` — (existing, unchanged — ProviderType and ContextOverflowStrategy already defined)
      - `Dtos.cs` — (existing, unchanged)
  - `MySecondBrain.Services/`
    - `Encryption/`
      - `DpapiEncryptionService.cs` — **REAL IMPLEMENTATION**: Protect/Unprotect via `System.Security.Cryptography.ProtectedData`
    - `LLM/`
      - `OpenAIProvider.cs` — **REAL**: ValidateKeyAsync + ListModelsAsync with HttpClient
      - `AnthropicProvider.cs` — **REAL**: ValidateKeyAsync + ListModelsAsync with HttpClient
      - `GoogleProvider.cs` — **REAL**: ValidateKeyAsync + ListModelsAsync with HttpClient
      - `OpenAICompatibleProvider.cs` — **REAL**: ValidateKeyAsync with configurable endpoint
      - `LLMProviderService.cs` — **REAL**: ValidateApiKeyAsync delegates to provider, ListModelsAsync delegates
  - `MySecondBrain.Data/`
    - `Entities/`
      - `ApiKey.cs` — (existing, unchanged)
      - `ModelConfiguration.cs` — (existing, unchanged)
      - `Persona.cs` — (existing, unchanged)
    - `Repositories/`
      - `ApiKeyRepository.cs` — **UPDATED**: UpdateAsync handles CustomProviderName, CustomEndpointUrl, IsValid, LastTestedAt; MapToDomain/MapToEntity include new fields
      - `ModelConfigurationRepository.cs` — **UPDATED**: UpdateAsync maps ApiKeyId, MaxContextWindow, Pricing, ContextOverflowStrategy; MapToDomain/MapToEntity include all fields
      - `PersonaRepository.cs` — **UPDATED**: UpdateAsync maps DefaultModelConfigId, DefaultChatMode; MapToDomain/MapToEntity include all fields
  - `MySecondBrain.UI/`
    - `ViewModels/`
      - `SettingsViewModel.cs` — **EXPANDED**: ApiKeys collection, ModelConfigs, Personas, CRUD commands, test key, category navigation, provider dropdown, model fetch
      - `ChatThreadViewModel.cs` — **EXPANDED**: ActivePersona, PersonaList, recently-used ordering, Ctrl+N picker
    - `Views/`
      - `SettingsView.xaml` — **EXPANDED**: Category switching, Providers section (API key list + add/edit form), Profiles section (Model Config list + form, Persona list + form)
      - `ChatView.xaml` — **UPDATED**: Persona selector in toolbar bound to real data, persona indicator in header
- `tests/`
  - `unit/MySecondBrain.Tests.Unit/`
    - `EncryptionTests.cs` — **NEW**: DPAPI encrypt/decrypt round-trip, cross-session protection
    - `ProviderTests.cs` — **NEW**: Key validation flow, model list fetch, OpenAI-Compatible endpoint resolution
    - `DataLayerTests.cs` — **EXPANDED**: New field mappings in ApiKey, ModelConfiguration, Persona repos
  - `integration/MySecondBrain.Tests.Integration/`
    - `ProviderIntegrationTests.cs` — **NEW**: End-to-end key validation against real API endpoints

---

## 5. Execution Steps

### [x] Step 1: Real DPAPI Encryption + Domain Model Completion + Repository Field Mapping
- **Goal:** Replace the DpapiEncryptionService stub with real Windows DPAPI encryption, complete all three domain models (ApiKey, ModelConfiguration, Persona) with missing fields matching the entity specs, and fix repository mapping gaps to ensure all entity fields round-trip correctly through the domain layer.
- **Actions:**
  - Implement `DpapiEncryptionService.Protect()`/`Unprotect()` using `System.Security.Cryptography.ProtectedData.Protect()`/`Unprotect()` with `DataProtectionScope.CurrentUser`
  - Implement `ProtectString()`/`UnprotectString()` as Base64 wrappers around the byte[] methods
  - Add missing fields to `Core/Models/DomainModels.cs` `ApiKey` domain model: `CustomProviderName` (string?), `CustomEndpointUrl` (string?), `CreatedAt` (DateTimeOffset), `LastTestedAt` (DateTimeOffset?), `IsValid` (bool)
  - Add missing fields to `ModelConfiguration` domain model: `ApiKeyId` (string?), `MaxContextWindow` (int, default 128000), `PricingInputPer1K` (decimal?), `PricingOutputPer1K` (decimal?), `ContextOverflowStrategy` (string, default "SlidingWindow"), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset)
  - Add missing fields to `Persona` domain model: `DefaultModelConfigId` (string?), `DefaultChatMode` (string, default "Standard"), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset)
  - Update `ApiKeyRepository.UpdateAsync()` to map `CustomProviderName`, `CustomEndpointUrl`, `IsValid`, `LastTestedAt`
  - Update `ApiKeyRepository.MapToDomain()`/`MapToEntity()` to include new fields
  - Fix `ModelConfigurationRepository.UpdateAsync()` — remove erroneous `ThinkingTokens` reference, add proper `ApiKeyId`, `MaxContextWindow`, pricing, `ContextOverflowStrategy` mapping
  - Update `ModelConfigurationRepository.MapToDomain()`/`MapToEntity()` to include all fields
  - Update `PersonaRepository.UpdateAsync()` to map `DefaultModelConfigId`, `DefaultChatMode`
  - Update `PersonaRepository.MapToDomain()`/`MapToEntity()` to include new fields
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/EncryptionTests.cs`: Test `Protect`/`Unprotect` round-trip with byte arrays, test `ProtectString`/`UnprotectString` round-trip, test `Unprotect` with tampered data throws `CryptographicException`, test that encrypting same plaintext twice produces different ciphertext (DPAPI uses random salt), test with empty string, test with long string (>1000 chars)
  - `tests/unit/MySecondBrain.Tests.Unit/DataLayerTests.cs` (expand): Test `ApiKeyRepository.CreateAsync` with all new fields (CustomProviderName, CustomEndpointUrl), test `ApiKeyRepository.UpdateAsync` updates CustomProviderName/CustomEndpointUrl/IsValid/LastTestedAt, test `ModelConfigurationRepository.CreateAsync` with ApiKeyId/MaxContextWindow/Pricing/ContextOverflowStrategy, test `PersonaRepository.CreateAsync` with DefaultModelConfigId/DefaultChatMode, test `PersonaRepository.UpdateAsync` updates DefaultModelConfigId/DefaultChatMode
- **Integration Tests to Write:** None — unit tests with in-memory SQLite cover all data layer changes
- **Automated Test Commands:** `dotnet test tests/unit/MySecondBrain.Tests.Unit`, `dotnet build`
- **Live Smoke Test (Mandatory):** Build the binary (`dotnet build`), then use a quick console test: run `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "FullyQualifiedName~EncryptionTests"` and verify all encryption round-trip tests pass with real DPAPI (not stubs). Verify `dotnet test --filter "FullyQualifiedName~ApiKeyRepository"` passes for new field mappings.
- **Smoke Test Classification:** Model
- **Suggested Commit Message:** `feat: implement real DPAPI encryption, complete domain models, fix repository field mappings`

---

### [x] Step 2: LLM Provider Key Validation & Model Fetching
- **Goal:** Replace the stub `ValidateKeyAsync` and `ListModelsAsync` methods in all 4 LLM providers with real HTTP calls to each provider's API. Wire `LLMProviderService` to delegate to the correct provider via the factory. Each provider validates by sending a minimal request (listing models for OpenAI/Anthropic/Google, health check for OpenAICompatible). Model fetching hits the provider's `/models` endpoint and caches results.
- **Actions:**
  - Add `System.Net.Http.HttpClient` usage to `MySecondBrain.Services.csproj` (if not already present via implicit usings; .NET 8 includes it)
  - Implement `OpenAIProvider.ValidateKeyAsync()`: GET `https://api.openai.com/v1/models` with `Authorization: Bearer {apiKey}`, return true on 200, false on 401/403
  - Implement `OpenAIProvider.ListModelsAsync()`: GET `https://api.openai.com/v1/models`, parse JSON response (`data[].id`), return `List<ModelInfo>`
  - Implement `AnthropicProvider.ValidateKeyAsync()`: GET `https://api.anthropic.com/v1/models` with `x-api-key: {apiKey}` and `anthropic-version: 2023-06-01`, return true on 200
  - Implement `AnthropicProvider.ListModelsAsync()`: same endpoint, parse response
  - Implement `GoogleProvider.ValidateKeyAsync()`: GET `https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}`, return true on 200
  - Implement `GoogleProvider.ListModelsAsync()`: same endpoint, parse `models[].name` (extract model ID after `models/`)
  - Implement `OpenAICompatibleProvider.ValidateKeyAsync()`: GET `{endpointUrl}/models` with optional `Authorization: Bearer {apiKey}`, return true on 200; also accept 200 without auth (local servers)
  - For OpenAICompatible: `ListModelsAsync()` returns empty (B6 spec: "No auto-fetch — user always enters manually")
  - Implement `LLMProviderService.ValidateApiKeyAsync()`: resolve provider from factory, call `ValidateKeyAsync` with the decrypted key
  - Implement `LLMProviderService.ListModelsAsync()`: resolve provider from factory, call `ListModelsAsync`
  - Add error handling: network timeout → return false with logged warning; non-2xx response → return false
  - Add `ILogger` diagnostics for all HTTP calls
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/ProviderTests.cs`: Test `OpenAIProvider.ValidateKeyAsync` returns false for invalid key format, test `OpenAIProvider.ListModelsAsync` handles HTTP timeout gracefully, test `LLMProviderService.ValidateApiKeyAsync` resolves correct provider by ProviderType, test `OpenAICompatibleProvider.ValidateKeyAsync` constructs correct URL from endpointUrl parameter, test all 4 providers have correct `Type` and `ProviderName` properties, test providers resolve from DI as `IEnumerable<ILLMProvider>`
- **Integration Tests to Write:**
  - `tests/integration/MySecondBrain.Tests.Integration/ProviderIntegrationTests.cs`: Test `OpenAIProvider.ValidateKeyAsync` with a real API key (read from environment variable `MSB_TEST_OPENAI_KEY`), test `AnthropicProvider.ValidateKeyAsync` with real key (`MSB_TEST_ANTHROPIC_KEY`), test `OpenAIProvider.ListModelsAsync` returns non-empty list
- **Automated Test Commands:** `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "FullyQualifiedName~ProviderTests"`, `dotnet test tests/integration/MySecondBrain.Tests.Integration --filter "FullyQualifiedName~ProviderIntegrationTests"`
- **Live Smoke Test (Mandatory):** Set `MSB_TEST_OPENAI_KEY` environment variable to a valid OpenAI API key. Run `dotnet test tests/integration/MySecondBrain.Tests.Integration --filter "FullyQualifiedName~ProviderIntegrationTests"`. Verify `ValidateKeyAsync` returns true with green output and `ListModelsAsync` returns a non-empty list containing model IDs like "gpt-4o". Verify `ValidateKeyAsync` returns false when given "sk-invalid-test-key".
- **Smoke Test Classification:** Model
- **Suggested Commit Message:** `feat: implement real provider key validation and model fetching for all 4 LLM providers`

---

### [x] Step 3: Settings UI — Providers Section (API Key Management)
- **Goal:** Build the Providers settings UI with full API key CRUD. The user navigates to Settings → Providers, sees a list of existing keys (masked with copy button), clicks "Add API Key" to open a form with provider dropdown (8 types), enters the key, clicks "Test Key" to validate, sees green checkmark or red error, and saves. Keys display masked (e.g., "sk-...abc123") with a copy-to-clipboard button. Edit and Delete with confirmation dialog.
- **Actions:**
  - Add `SelectedSettingsCategory` enum-backed property to `SettingsViewModel` with initial value `Providers`
  - Add `ObservableCollection<ApiKeyDisplayItem>` for the key list, with `MaskedValue` computed property (first 3 + "..." + last 6 chars)
  - Add commands: `AddApiKeyCommand`, `SaveApiKeyCommand`, `TestApiKeyCommand`, `DeleteApiKeyCommand`, `CopyKeyCommand`
  - Add form properties: `EditingApiKey` (ApiKey domain model), `SelectedProviderType`, `ApiKeyInputValue` (plaintext before encryption), `TestResultMessage`, `IsTestSuccess`, `IsTesting`
  - Wire `TestApiKeyCommand` to `ILLMProviderService.ValidateApiKeyAsync` — encrypt via `IEncryptionService`, then validate
  - Wire `SaveApiKeyCommand` to encrypt via `IEncryptionService.ProtectString()`, create/update via `IApiKeyRepository`
  - Inject `IApiKeyRepository`, `IEncryptionService`, `ILLMProviderService` into `SettingsViewModel`
  - Register new dependencies in `App.xaml.cs` `ConfigureServices` if needed (repos already registered)
  - Build XAML: Providers category pane in SettingsView column 1 (replacing "Select a category" placeholder)
  - XAML layout: left list (ScrollViewer with masked keys + copy button), right form (provider dropdown, key input PasswordBox, display name TextBox, Test Key button with result indicator, Save/Cancel buttons)
  - Provider dropdown: ComboBox with 8 items (OpenAI, Anthropic, Google, DeepSeek, MiMo, Moonshot, Mistral, OpenAI-Compatible)
  - When OpenAI-Compatible selected: show CustomProviderName and CustomEndpointUrl fields
  - Copy button: uses `Clipboard.SetText()` to copy the raw (decrypted) key value — decrypt via `IEncryptionService.UnprotectString()`
  - Delete confirmation: `MessageBox` showing "Delete this API key? Any Model Configurations using it will need a new key."
  - After successful save: refresh key list, clear form, show brief "API key saved" status
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/SettingsViewModelTests.cs`: Test `MaskedValue` computation ("sk-abc123def456" → "sk-...f456"), test `AddApiKeyCommand` clears form and sets edit mode, test `SaveApiKeyCommand` calls `IEncryptionService.ProtectString` and `IApiKeyRepository.CreateAsync`, test `TestApiKeyCommand` sets `IsTestSuccess=true` on valid key, test `TestApiKeyCommand` sets error message on invalid key, test `DeleteApiKeyCommand` calls `IApiKeyRepository.DeleteAsync`, test `CopyKeyCommand` decrypts via `IEncryptionService.UnprotectString`, test provider dropdown selection shows/hides OpenAICompatible fields
- **Integration Tests to Write:** None — UI logic tested via unit tests with mocked services
- **Automated Test Commands:** `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "FullyQualifiedName~SettingsViewModelTests"`, `dotnet build`
- **Live Smoke Test (Mandatory):** Launch the app (`dotnet run --project src/MySecondBrain.UI`), navigate to Settings → Providers (click sidebar item). Click "Add API Key". Select "OpenAI" from provider dropdown. Enter a test API key. Click "Test Key" — verify green checkmark and "API key validated successfully" message appears. Click "Save" — verify key appears in the list with masked display (e.g., "sk-...abc123"). Click the copy button — verify key is copied to clipboard (paste into Notepad to confirm). Click "Delete" and confirm dialog. Verify key is removed from list.
- **Smoke Test Classification:** HUMAN/SHT REQUIRED
- **Suggested Commit Message:** `feat: build Settings Providers UI with API key CRUD, DPAPI encryption, test validation, and masked display`

---

### [ ] Step 4: Settings UI — Profiles Section (Model Configurations & Personas)
- **Goal:** Build the Profiles settings UI with Model Configuration CRUD (B2) and Persona CRUD (B3). Under Settings → Profiles, the user sees two sub-sections: Model Configurations list and Personas list. Model Config form includes: display name, provider+model dropdown (auto-fetched), API key selector, temperature slider (0.0-2.0), max output tokens, max context window, thinking toggle, pricing fields, context overflow strategy. Persona form includes: display name, system prompt text area with {{variables}} hint, default model config dropdown, default chat mode radio buttons.
- **Actions:**
  - Extend `SettingsViewModel` with: `ObservableCollection<ModelConfigurationDisplayItem>` for model configs, `ObservableCollection<PersonaDisplayItem>` for personas
  - Add commands: `AddModelConfigCommand`, `SaveModelConfigCommand`, `DuplicateModelConfigCommand`, `DeleteModelConfigCommand`, `AddPersonaCommand`, `SavePersonaCommand`, `DeletePersonaCommand`
  - Add form properties: `EditingModelConfig`, `EditingPersona`, `AvailableModels` (from auto-fetch), `AvailableApiKeys`, `IsFetchingModels`
  - Implement `FetchModelsCommand` — calls `ILLMProviderService.ListModelsAsync` and populates `AvailableModels`
  - Auto-fetch models when provider or API key changes
  - Wire save commands to `IModelConfigurationRepository`/`IPersonaRepository`
  - `DuplicateModelConfigCommand`: copy current config with "(Copy)" suffix appended to DisplayName, clear Id, create as new
  - Build XAML: Profiles category pane in SettingsView
  - XAML layout: two sections (Model Configs, Personas) each with list + form
  - Model Config form fields: `DisplayName` TextBox, `Provider` ComboBox (driven by available API keys), `ApiKeyId` ComboBox, `ModelIdentifier` ComboBox (auto-fetched, with manual entry fallback), `Temperature` Slider (0.0-2.0, step 0.1, with value label), `MaxOutputTokens` NumericUpDown-style, `MaxContextWindow` NumericUpDown, `ThinkingEnabled` ToggleSwitch, `PricingInputPer1K`/`PricingOutputPer1K` TextBox (decimal), `ContextOverflowStrategy` ComboBox (SlidingWindow/HardStop/AutoSummarize)
  - Persona form fields: `DisplayName` TextBox, `SystemPrompt` multi-line TextBox with placeholder hinting at {{date}}, {{time}}, {{user_name}}, `DefaultModelConfigId` ComboBox (populated from saved model configs), `DefaultChatMode` RadioButtons (Standard/TextCompletion)
  - Delete confirmation dialogs: "This Model Configuration is used by [N] Personas..." or "Delete this Persona?"
  - `DuplicateModelConfigCommand`: creates a copy with "(Copy)" suffix, preserving all settings
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/SettingsViewModelTests.cs` (expand): Test `AddModelConfigCommand` initializes form with defaults (Temperature=1.0, ContextOverflowStrategy=SlidingWindow), test `SaveModelConfigCommand` calls `IModelConfigurationRepository.CreateAsync` with correct fields, test `DuplicateModelConfigCommand` appends "(Copy)" to DisplayName, test `DeleteModelConfigCommand` shows warning when referenced by Personas, test `AddPersonaCommand` initializes with Standard chat mode, test `SavePersonaCommand` calls `IPersonaRepository.CreateAsync`, test `FetchModelsCommand` populates `AvailableModels`, test temperature slider clamps to 0.0-2.0 range, test context overflow strategy defaults to SlidingWindow
- **Integration Tests to Write:** None — UI logic tested via unit tests with mocked services
- **Automated Test Commands:** `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "FullyQualifiedName~SettingsViewModelTests"`, `dotnet build`
- **Live Smoke Test (Mandatory):** Launch the app, navigate to Settings → Profiles. Verify "Model Configurations" and "Personas" sections are visible. Click "New Model Configuration". Set display name to "GPT-4o Test". Select a provider (must have an API key saved from Step 3). Verify model dropdown populates from auto-fetch. Set temperature slider to 0.7. Select SlidingWindow for context overflow. Click Save — verify it appears in the list. Click "Duplicate" — verify a copy appears with "(Copy)" suffix. Click "New Persona". Set name to "Test Persona", enter system prompt "You are a test assistant. Today is {{date}}.", select the saved model config, choose Standard chat mode, save. Verify persona appears in list. Delete the persona and model config — verify both are removed.
- **Smoke Test Classification:** HUMAN/SHT REQUIRED
- **Suggested Commit Message:** `feat: build Settings Profiles UI with Model Configuration and Persona CRUD, auto-fetch models, duplicate, temperature slider`

---

### [ ] Step 5: Persona Selection per Chat (B4)
- **Goal:** Wire persona selection into the Studio Chat screen. The hardcoded "Default Persona" header and "🤖 Default Persona ▾" toolbar button are replaced with live persona data. Ctrl+N opens a persona picker dialog. The in-chat persona dropdown shows all personas with recently-used ordering (top 5). The chat header displays the active persona name. Changing the persona mid-chat updates the system prompt and model config for subsequent messages.
- **Actions:**
  - Extend `ChatThreadViewModel` constructor to inject `IPersonaRepository`, `IModelConfigurationRepository`, `ILogger<ChatThreadViewModel>`
  - Add properties: `ActivePersona` (Persona domain model), `PersonaList` (ObservableCollection<Persona>), `ActiveModelConfig` (ModelConfiguration)
  - Add commands: `SelectPersonaCommand`, `OpenPersonaPickerCommand` (Ctrl+N)
  - Implement recently-used ordering: track last 5 selected persona IDs via `ISettingsRepository` key `"RecentPersonaIds"` (JSON array), sort `PersonaList` with recently-used at top
  - Bind persona dropdown in toolbar to `PersonaList` with `DisplayMemberPath="DisplayName"`
  - Bind chat header persona name to `ActivePersona.DisplayName`
  - Update `ChatView.xaml`: Replace hardcoded `TextBlock Text="Default Persona"` with `TextBlock Text="{Binding ActivePersona.DisplayName, FallbackValue='Select Persona'}"`
  - Replace hardcoded `Button Content="🤖 Default Persona ▾"` with `ComboBox ItemsSource="{Binding PersonaList}" SelectedItem="{Binding ActivePersona}"`
  - Implement `OpenPersonaPickerCommand`: show a simple dialog (Popup or Window) listing personas with search/filter, select on Enter/click
  - Register Ctrl+N global hotkey in `ChatView` or `MainWindow` keybindings that fires `OpenPersonaPickerCommand`
  - On persona change: update `ActiveModelConfig` from `ActivePersona.DefaultModelConfigId`, resolve system prompt {{variables}} (date/time/user_name) — placeholder resolution for now
  - Add `PersonaDisplayItem` wrapper if needed for recently-used visual indicator
  - When no persona is selected and user sends a message, default to the first built-in persona (via `IPersonaRepository.GetDefaultAsync()`)
- **Unit Tests to Write:**
  - `tests/unit/MySecondBrain.Tests.Unit/ChatThreadViewModelTests.cs`: Test `ActivePersona` defaults to built-in persona (General Assistant), test `SelectPersonaCommand` updates `ActivePersona` and `ActiveModelConfig`, test recently-used ordering places last-selected persona at top, test `OpenPersonaPickerCommand` filters persona list, test persona change resolves {{date}}/{{time}} in system prompt, test PersonaList is sorted with recently-used first
- **Integration Tests to Write:** None — UI logic tested via unit tests with mocked services
- **Automated Test Commands:** `dotnet test tests/unit/MySecondBrain.Tests.Unit --filter "FullyQualifiedName~ChatThreadViewModelTests"`, `dotnet build`
- **Live Smoke Test (Mandatory):** Launch the app. Navigate to Studio Chat (default screen). Verify the chat header shows the active persona name (should show "General Assistant" from built-in defaults). Click the persona dropdown in the toolbar — verify all personas from the Profiles section appear, with recently-used ones at top. Press Ctrl+N — verify the persona picker opens, showing all personas. Select a different persona — verify the chat header updates to show the new persona name. Verify the persona dropdown in the toolbar reflects the change. Select a persona, close the app, reopen — verify the persona persists.
- **Smoke Test Classification:** HUMAN/SHT REQUIRED
- **Suggested Commit Message:** `feat: wire persona selection into Studio Chat with Ctrl+N picker, recently-used ordering, and persona indicator in chat header`

---

## 6. Shared Technical Context
*(Append-only log managed by the Feature Developer. Stores API endpoints, JSON payloads, state shapes, and abstractions created in earlier steps that later steps might need to read).*
- [Initial State]: No shared context yet.
- **Step 1 — Domain Model Fields:** ApiKey gains: CustomProviderName (string?), CustomEndpointUrl (string?), CreatedAt (DateTimeOffset), LastTestedAt (DateTimeOffset?), IsValid (bool). ModelConfiguration gains: ApiKeyId (string?), MaxContextWindow (int, default 128000), PricingInputPer1K (decimal?), PricingOutputPer1K (decimal?), ContextOverflowStrategy (string, default "SlidingWindow"), CreatedAt (DateTimeOffset), UpdatedAt (DateTimeOffset). Persona gains: DefaultModelConfigId (string?), DefaultChatMode (string, default "Standard"), CreatedAt (DateTimeOffset), UpdatedAt (DateTimeOffset). DpapiEncryptionService real implementation using System.Security.Cryptography.ProtectedData with DataProtectionScope.CurrentUser, string methods as Base64 wrappers. All three repository MapToDomain/MapToEntity/UpdateAsync methods map all new fields. All CreateAsync methods use MapToEntity() — new fields not silently dropped.
- **Step 2 — Provider API Endpoints & Behaviors:** OpenAI: GET https://api.openai.com/v1/models (Bearer auth), parses data[].id. Anthropic: GET https://api.anthropic.com/v1/models (x-api-key + anthropic-version: 2023-06-01), parses data[].id. Google: GET https://generativelanguage.googleapis.com/v1beta/models?key= (query param), parses models[].name (strips "models/" prefix). OpenAICompatible: GET {endpointUrl}/models (optional Bearer), ListModelsAsync returns empty (B6 spec). IApiKeyRepository.GetByProviderAsync(ProviderType) added. ILLMProvider.ValidateKeyAsync gains 3-param overload (string, CancellationToken, string?). LLMProviderService.ValidateApiKeyAsync/ListModelsAsync delegate to factory-resolved provider. ApiKeyHelper.MaskKey() shared utility for safe logging.
- **Step 3 — Settings UI Providers Abstractions:** SettingsCategory enum (15 values: Providers, Profiles, Appearance...). ApiKeyDisplayItem wrapper with MaskPlaintext() static utility. SettingsCategoryItem record (Icon+Label+Category tuple). IConfirmationService / WpfConfirmationService for mockable confirmation dialogs. SettingsViewModel now exposes: FormTitle, SelectedSettingsCategory, ApiKeys (ObservableCollection), IsEditingKey, EditingApiKey, SelectedProviderType, ApiKeyInputValue, DisplayNameInputValue, CustomProviderNameValue, CustomEndpointUrlValue, IsOpenAiCompatibleSelected, TestResultMessage, IsTestSuccess, IsTesting, StatusMessage. SettingsView.xaml fully data-bound with category sidebar ListBox, Provider-specific form with DataTrigger for OpenAI-Compatible fields, key list with valid/invalid indicators.
