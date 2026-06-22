# Feature Reference: Model Configurations, API Keys & Personas

## Global & Shared Documentation
- **System.Security.Cryptography.ProtectedData** — Windows DPAPI for encrypting/decrypting API key values. Uses `DataProtectionScope.CurrentUser` so keys are tied to the local Windows user account. Available in `System.Security.Cryptography.ProtectedData` NuGet package (included with .NET 8 Windows targeting).
- **EF Core + SQLite** — See `agent-workspace/external-docs/ef-core-sqlite.md` for the existing data layer patterns. All repositories follow the entity↔domain mapping pattern with `MapToDomain()`/`MapToEntity()` helpers.
- **CommunityToolkit.Mvvm** — `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]` for ViewModel bindings. See knowledge/architecture.md §2.1.
- **Serilog** — `ILogger<T>` via `Microsoft.Extensions.Logging` bridge. All new services must inject `ILogger<T>` in constructor.
- **`[REDACTED]` Logging Policy** — API key values MUST be redacted in all diagnostic log output. Never log raw key values. Log only masked representations (e.g., `"sk-...abc123"`). The `keyValue` column comment in `ApiKey` entity specifies: `Must be redacted ([REDACTED]) in all diagnostic log output via Serilog destructuring policy (V1)`.

---

## Step-Specific Documentation

### Step 1: Real DPAPI Encryption + Domain Model Completion + Repository Field Mapping

- **Library:** `System.Security.Cryptography.ProtectedData`
- **Import:** `using System.Security.Cryptography;`
- **Snippet:**
```csharp
// Protect (encrypt) — returns Base64-encoded ciphertext
public string ProtectString(string plaintext)
{
    if (string.IsNullOrEmpty(plaintext))
        return string.Empty;

    var plainBytes = Encoding.UTF8.GetBytes(plaintext);
    var cipherBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null,
        DataProtectionScope.CurrentUser);
    return Convert.ToBase64String(cipherBytes);
}

// Unprotect (decrypt) — returns original plaintext
public string UnprotectString(string ciphertext)
{
    if (string.IsNullOrEmpty(ciphertext))
        return string.Empty;

    var cipherBytes = Convert.FromBase64String(ciphertext);
    var plainBytes = ProtectedData.Unprotect(cipherBytes, optionalEntropy: null,
        DataProtectionScope.CurrentUser);
    return Encoding.UTF8.GetString(plainBytes);
}
```

- **Key behaviors:**
  - `ProtectedData.Protect` produces different ciphertext for the same plaintext on each call (DPAPI uses random salt internally)
  - `ProtectedData.Unprotect` throws `CryptographicException` if ciphertext is tampered with
  - `DataProtectionScope.CurrentUser` ties encryption to the current Windows user — decryption fails if the database file is moved to a different machine or user account
  - Empty/null strings are passed through without encryption (returned as-is)

- **Domain model fields to add to `ApiKey` (Core/Models/DomainModels.cs):**
```csharp
public string? CustomProviderName { get; set; }
public string? CustomEndpointUrl { get; set; }
public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
public DateTimeOffset? LastTestedAt { get; set; }
public bool IsValid { get; set; }
```

- **Domain model fields to add to `ModelConfiguration` (Core/Models/DomainModels.cs):**
```csharp
public string? ApiKeyId { get; set; }
public int MaxContextWindow { get; set; } = 128000;
public decimal? PricingInputPer1K { get; set; }
public decimal? PricingOutputPer1K { get; set; }
public string ContextOverflowStrategy { get; set; } = "SlidingWindow";
public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
```

- **Domain model fields to add to `Persona` (Core/Models/DomainModels.cs):**
```csharp
public string? DefaultModelConfigId { get; set; }
public string DefaultChatMode { get; set; } = "Standard";
public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
```

- **Repository mapping fix — `ApiKeyRepository.UpdateAsync` must also set:**
```csharp
entity.CustomProviderName = key.CustomProviderName;
entity.CustomEndpointUrl = key.CustomEndpointUrl;
entity.IsValid = key.IsValid;
entity.LastTestedAt = key.LastTestedAt;
```

- **Repository mapping fix — `ModelConfigurationRepository.UpdateAsync` must fix:**
  - Remove: `entity.MaxContextWindow = config.ThinkingTokens ?? config.MaxContextWindow;` (ThinkingTokens is a domain-model-only concept not stored in the entity)
  - Add: `entity.ApiKeyId = config.ApiKeyId;`
  - Add: `entity.MaxContextWindow = config.MaxContextWindow;`
  - Add: `entity.PricingInputPer1K = config.PricingInputPer1K;`
  - Add: `entity.PricingOutputPer1K = config.PricingOutputPer1K;`
  - Add: `entity.ContextOverflowStrategy = config.ContextOverflowStrategy;`

- **Repository mapping fix — `PersonaRepository.UpdateAsync` must also set:**
```csharp
entity.DefaultModelConfigId = persona.DefaultModelConfigId;
entity.DefaultChatMode = persona.DefaultChatMode;
```

- **Existing test framework:** xUnit 2.x with `[Fact]` and `[StaFact]` (for WPF-thread-affine tests). Test project: `tests/unit/MySecondBrain.Tests.Unit`. Uses in-memory SQLite (`DataSource=:memory:`) via `CreateTestDbContext()` helper. Mocking: Moq (4.x).

---

### Step 2: LLM Provider Key Validation & Model Fetching

- **Library:** `System.Net.Http.HttpClient` (included in .NET 8 implicit usings for Windows-targeted projects)
- **Library:** `System.Text.Json` (for JSON response parsing — included in .NET 8)

- **OpenAI — Validate Key & List Models:**
```
Endpoint: GET https://api.openai.com/v1/models
Auth: Authorization: Bearer {apiKey}
Success: HTTP 200
Failure: HTTP 401 (invalid key), HTTP 403 (permissions), HTTP 429 (rate limit)
Response: {"object":"list","data":[{"id":"gpt-4o","object":"model","created":1686935002,"owned_by":"openai"},...]}
Parse: data[].id for model identifiers
```

- **Anthropic — Validate Key & List Models:**
```
Endpoint: GET https://api.anthropic.com/v1/models
Auth: x-api-key: {apiKey}
Required header: anthropic-version: 2023-06-01
Success: HTTP 200
Failure: HTTP 401 (invalid key)
Response: {"data":[{"id":"claude-sonnet-4-20250514","display_name":"Claude Sonnet 4","type":"model",...}],...}
Parse: data[].id for model identifiers
```

- **Google Gemini — Validate Key & List Models:**
```
Endpoint: GET https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}
Auth: Query parameter `key={apiKey}` (NOT header-based)
Success: HTTP 200
Failure: HTTP 400 (invalid key)
Response: {"models":[{"name":"models/gemini-2.5-flash","displayName":"Gemini 2.5 Flash",...}],...}
Parse: models[].name, extract portion after "models/" as the model identifier
```

- **OpenAI-Compatible — Validate Key Only (no model fetch):**
```
Endpoint: GET {customEndpointUrl}/models
Auth: Authorization: Bearer {apiKey} (optional — local servers may not require it)
Success: HTTP 200 (also accept any non-401/403 response as success for lenient local servers)
No model fetching — B6 spec: "No auto-fetch — user always enters manually"
```

- **Provider implementation pattern (all 4 providers follow this):**
```csharp
public async Task<bool> ValidateKeyAsync(string apiKey, CancellationToken ct)
{
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        using var response = await http.SendAsync(request, ct);
        _logger.LogDebug("Key validation for {Provider}: HTTP {Status}", ProviderName, (int)response.StatusCode);
        return response.IsSuccessStatusCode;
    }
    catch (HttpRequestException ex)
    {
        _logger.LogWarning(ex, "Network error during key validation for {Provider}", ProviderName);
        return false;
    }
    catch (TaskCanceledException) // timeout
    {
        _logger.LogWarning("Timeout during key validation for {Provider}", ProviderName);
        return false;
    }
}
```

- **LLMProviderService.ValidateApiKeyAsync bridge pattern:**
```csharp
public async Task<bool> ValidateApiKeyAsync(ProviderType provider, string apiKey,
    string? endpointUrl, CancellationToken ct)
{
    var llmProvider = _providerFactory.GetProvider(provider, endpointUrl);
    return await llmProvider.ValidateKeyAsync(apiKey, ct);
}
```

- **Integration test environment variable convention:**
  - `MSB_TEST_OPENAI_KEY` — valid OpenAI API key for integration tests
  - `MSB_TEST_ANTHROPIC_KEY` — valid Anthropic API key
  - If not set, integration tests are skipped via `[SkippableFact]` or environment check in test body

---

### Step 3: Settings UI — Providers Section (API Key Management)

- **Library:** WPF (existing), CommunityToolkit.Mvvm (existing)
- **Library:** `System.Windows.Clipboard` (WPF built-in) for copy-to-clipboard

- **SettingsViewModel new dependencies to inject:**
```csharp
public SettingsViewModel(
    ISettingsRepository settingsRepo,
    IThemeProvider themeProvider,
    IApiKeyRepository apiKeyRepo,          // NEW
    IEncryptionService encryptionService,    // NEW
    ILLMProviderService llmProviderService,  // NEW
    ILogger<SettingsViewModel> logger)
```

- **ApiKeyDisplayItem wrapper for masked display:**
```csharp
public partial class ApiKeyDisplayItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ProviderType ProviderType { get; set; }
    public string EncryptedValue { get; set; } = string.Empty; // stored for copy
    public bool IsValid { get; set; }

    public string MaskedValue => ComputeMaskedValue(EncryptedValue);

    private static string ComputeMaskedValue(string encrypted)
    {
        // For display: show "sk-...abc123" or "****" if cannot decrypt for display
        // The actual masking is applied at the ViewModel level using decrypted value
        return "••••••••"; // placeholder — actual mask shows first 3 + "..." + last 4 of decrypted key
    }
}
```

- **SelectedSettingsCategory enum (add to Core/Models/Enums.cs or SettingsViewModel):**
```csharp
public enum SettingsCategory
{
    Providers,
    Profiles,
    Appearance,
    Wiki,
    Backup,
    TextActions,
    Hotkeys,
    Tools,
    Notifications,
    Startup,
    Updates,
    Pricing,
    Security,
    Maintenance,
    Diagnostics
}
```

- **SettingsView XAML — category switching pattern:**
  - The existing `ListBox` in SettingsView sidebar will bind `SelectedItem` to `SelectedSettingsCategory`
  - Column 1 content area will use a `ContentControl` with `DataTemplate` per category (or direct visibility toggling)
  - For Step 3, implement the Providers `DataTemplate` only; other categories remain "Select a category" placeholder

- **PasswordBox for API key input:** WPF `PasswordBox` for secure key entry (text is not stored in memory as plain string in the visual tree). Access via `PasswordBox.Password` property in code-behind or via attached behavior.

- **Test Key button flow:**
  1. Read plaintext from `PasswordBox`
  2. Set `IsTesting = true`, clear previous result
  3. Call `ILLMProviderService.ValidateApiKeyAsync(provider, plaintext, endpointUrl, ct)`
  4. On success: `IsTestSuccess = true`, `TestResultMessage = "API key validated successfully. N models available."` (optionally fetch model count)
  5. On failure: `IsTestSuccess = false`, `TestResultMessage = "API key validation failed. (401) Invalid API key."`
  6. Set `IsTesting = false`

- **Save Key flow:**
  1. Encrypt plaintext via `IEncryptionService.ProtectString(plaintext)`
  2. Create `ApiKey` domain model with encrypted value
  3. Call `IApiKeyRepository.CreateAsync(key)` or `UpdateAsync(key)`
  4. Refresh key list from `IApiKeyRepository.GetAllAsync()`

- **Copy Key flow:**
  1. Decrypt stored `EncryptedValue` via `IEncryptionService.UnprotectString(encryptedValue)`
  2. Call `Clipboard.SetText(decryptedValue)`
  3. Optionally show brief "Copied!" tooltip

---

### Step 4: Settings UI — Profiles Section (Model Configurations & Personas)

- **Library:** WPF (existing), CommunityToolkit.Mvvm (existing)
- **New ViewModel dependencies:** `IModelConfigurationRepository`, `IPersonaRepository`

- **Temperature Slider XAML pattern:**
```xml
<StackPanel Orientation="Horizontal">
    <Slider Minimum="0" Maximum="2" SmallChange="0.1" LargeChange="0.2"
            Value="{Binding EditingModelConfig.Temperature}" Width="200"
            TickFrequency="0.1" IsSnapToTickEnabled="True"/>
    <TextBlock Text="{Binding EditingModelConfig.Temperature, StringFormat={}{0:F1}}"
               Width="30" TextAlignment="Center" VerticalAlignment="Center"/>
</StackPanel>
```

- **ContextOverflowStrategy ComboBox:**
```xml
<ComboBox ItemsSource="{Binding ContextOverflowStrategyOptions}"
          SelectedItem="{Binding EditingModelConfig.ContextOverflowStrategy}"/>
```
Options: "SlidingWindow" (display: "Sliding Window"), "HardStop" (display: "Hard Stop"), "AutoSummarize" (display: "Auto-Summarize")

- **Model fetching integration:**
  - When provider changes on Model Config form, auto-trigger: find the API key for that provider, decrypt it, call `ILLMProviderService.ListModelsAsync`
  - Populate `AvailableModels` ObservableCollection with results
  - Manual "Refresh" button also available
  - Show spinner/progress indicator during fetch (`IsFetchingModels = true`)

- **Duplicate command:**
```csharp
[RelayCommand]
private void DuplicateModelConfig(ModelConfigurationDisplayItem source)
{
    var copy = new ModelConfiguration
    {
        Id = Guid.NewGuid().ToString("N"), // new ID
        DisplayName = source.DisplayName + " (Copy)",
        ProviderType = source.ProviderType,
        ApiKeyId = source.ApiKeyId,
        ModelIdentifier = source.ModelIdentifier,
        Temperature = source.Temperature,
        MaxOutputTokens = source.MaxOutputTokens,
        MaxContextWindow = source.MaxContextWindow,
        ThinkingEnabled = source.ThinkingEnabled,
        PricingInputPer1K = source.PricingInputPer1K,
        PricingOutputPer1K = source.PricingOutputPer1K,
        ContextOverflowStrategy = source.ContextOverflowStrategy,
    };
    EditingModelConfig = copy;
    // Auto-save or let user edit then save
}
```

- **System prompt {{variables}} hint:** Display a static text hint below the system prompt text area:
  "Available variables: {{`{{date}}`}}, {{`{{time}}`}}, {{`{{user_name}}`}} — resolved at message send time"

- **Persona DefaultChatMode radio buttons:**
```xml
<RadioButton Content="Standard" IsChecked="{Binding EditingPersona.DefaultChatMode, 
    Converter={StaticResource EnumMatchConverter}, ConverterParameter=Standard}"/>
<RadioButton Content="Text Completion" IsChecked="{Binding EditingPersona.DefaultChatMode,
    Converter={StaticResource EnumMatchConverter}, ConverterParameter=TextCompletion}"/>
```

---

### Step 5: Persona Selection per Chat (B4)

- **Library:** WPF (existing), CommunityToolkit.Mvvm (existing)
- **New ChatThreadViewModel dependencies:** `IPersonaRepository`, `IModelConfigurationRepository`

- **Recently-used persona IDs storage:**
  - Key: `"RecentPersonaIds"` in `ISettingsRepository`
  - Value: JSON array of persona IDs, e.g., `["abc123","def456","ghi789"]`
  - Max 5 entries; new selection pushes to front, removes oldest if >5

- **PersonaList sorting pattern:**
```csharp
private void RefreshPersonaList()
{
    var allPersonas = await _personaRepo.GetAllAsync();
    var recentIds = await _settingsRepo.GetAsync<List<string>>("RecentPersonaIds") ?? [];

    // Recently used first, then alphabetically
    var sorted = allPersonas
        .OrderByDescending(p => recentIds.IndexOf(p.Id)) // -1 sorts last
        .ThenBy(p => p.DisplayName)
        .ToList();

    PersonaList = new ObservableCollection<Persona>(sorted);
    ActivePersona = PersonaList.FirstOrDefault();
}
```

- **Persona picker dialog (Ctrl+N):**
  - A simple `Window` or `Popup` with a `TextBox` for search/filter and a `ListBox` for results
  - Filter as user types (case-insensitive contains match on DisplayName)
  - Select via click or Enter key
  - Close on Escape

- **ChatView.xaml header binding change:**
  - Replace: `<TextBlock Text="Default Persona" .../>`
  - With: `<TextBlock Text="{Binding ActivePersona.DisplayName, FallbackValue='Select Persona'}" .../>`

- **ChatView.xaml toolbar persona dropdown change:**
  - Replace: `<Button Content="🤖 Default Persona ▾" .../>`
  - With:
```xml
<ComboBox ItemsSource="{Binding PersonaList}"
          SelectedItem="{Binding ActivePersona}"
          DisplayMemberPath="DisplayName"
          FontSize="10" Width="180"
          AutomationProperties.AutomationId="PersonaSelector"/>
```

- **System prompt variable resolution (placeholder for this step):**
```csharp
private string ResolveSystemPrompt(string template)
{
    return template
        .Replace("{{date}}", DateTime.Now.ToString("yyyy-MM-dd"))
        .Replace("{{time}}", DateTime.Now.ToString("HH:mm:ss"))
        .Replace("{{user_name}}", Environment.UserName);
}
```
(This is a basic implementation; future features may add more variables or a proper template engine.)
