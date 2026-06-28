# Technology Sourcing Update — June 2026

**Context:** Between Feature 11 (complete) and Feature 12 (Studio Chat Core). No LLM provider HTTP client code exists yet — this is the sourcing window.

**Methodology:** Each item was investigated via NuGet.org (download counts, .NET target frameworks, last update), GitHub (stars, issues, commit activity, archive status), and Context7 documentation where available. Findings are evidence-based; recommendations are pragmatic for a native Windows WPF .NET 8 desktop app.

---

## Item 1 — Microsoft.Extensions.AI (MEAI) Adoption

### Finding

| Metric | Value |
|--------|-------|
| Package | [`Microsoft.Extensions.AI`](https://www.nuget.org/packages/Microsoft.Extensions.AI) v10.7.0 |
| Total Downloads | **18.6M** |
| .NET 8 Compatible | ✅ Yes (net8.0 target) |
| Last Updated | June 9, 2026 (3 weeks ago) |
| License | MIT |
| Maintainer | Microsoft (dotnet/extensions) |
| GitHub Stars | Part of dotnet/extensions (6.1K+ stars for Aspire ecosystem) |

MEAI provides [`IChatClient`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.ichatclient) as the central abstraction, [`ChatClientBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.chatclientbuilder) for middleware pipelines (`UseFunctionInvocation`, `UseDistributedCache`, `UseOpenTelemetry`, `UseLogging`), and [`FunctionInvokingChatClient`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.functioninvokingchatclient) for automatic tool-calling loops. DI integration via `AddChatClient()`.

#### 1a. HSO.Extensions.AI Ecosystem Health

**Finding: The HSO.Extensions.AI ecosystem could not be verified.** No NuGet packages found under `HSO.Extensions.AI.*` naming. No GitHub repository found at `haath/HSO.Extensions.AI`. These community packages (GoogleChat, DeepSeekChat, MistralChat, QwenChat) appear to not exist on NuGet or have been renamed/removed. **This is a critical finding** — the provider ecosystem around MEAI for non-OpenAI providers is thinner than assumed.

Available MEAI-compatible providers verified:
- **OpenAI + OpenAI-compatible:** [`Microsoft.Extensions.AI.OpenAI`](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI) — Official Microsoft, v10.7.0, 3.2M downloads
- **Ollama:** [`OllamaSharp`](https://www.nuget.org/packages/OllamaSharp) — Implements `IChatClient`, 1.4K GitHub stars, active
- **Anthropic:** Official C# SDK at [`Anthropic`](https://www.nuget.org/packages/Anthropic) NuGet — needs verification of `IChatClient` implementation

#### 1b. Anthropic SDK IChatClient Implementation

**Finding: Unclear without direct code inspection.** The official Anthropic .NET SDK may or may not implement `IChatClient`. Even if it does, Anthropic-specific features (extended thinking with `thinking.budget_tokens`, beta headers like `anthropic-beta: prompt-caching-2024-07-31`, `anthropic-beta: max-tokens-3-5-sonnet-2024-07-15`) likely require bypassing the `IChatClient` abstraction to access provider-specific request/response properties. This is the classic "lowest common denominator" problem with abstractions.

#### 1c. FunctionInvokingChatClient Fit

`FunctionInvokingChatClient` provides automatic tool-calling loops using `AIFunctionFactory.Create()`. **It is designed for server-side .NET function invocation, not desktop UX-confirmation workflows.** Specifically:

| Requirement | MEAI Fit | Assessment |
|-------------|----------|-----------|
| (a) Pre-execution user confirmation dialogs | ❌ No built-in support | `FunctionInvokingChatClient` auto-executes — no hook for confirmation UI |
| (b) Per-tool auto-approval toggle | ❌ No built-in support | Binary: tool is available or not; no "auto-approve vs. ask vs. disabled" tri-state |
| (c) Anthropic provider-specific tool schemas | ⚠️ Partial | MEAI normalizes tool schemas to a common format — provider-specific nuances lost |
| (d) Streaming tool results to UI | ⚠️ Partial | `IChatClient` supports streaming via `GetStreamingResponseAsync()`, but tool execution is blocking within the pipeline |

**Estimated `IToolOrchestrator` replacement: ~20-30%.** MEAI could replace the low-level LLM call + tool schema serialization, but the confirmation workflow, per-tool approval toggles, parallel execution display, and WPF confirmation dialogs remain custom code. Our `IToolOrchestrator` is thin orchestration glue — MEAI's `FunctionInvokingChatClient` solves a different problem (server-side auto-execution).

#### 1d. Hybrid Approach Risk Assessment

A hybrid approach — MEAI for `IChatClient` + middleware (caching, OTEL, logging), provider SDKs directly for model listing, key validation, and Anthropic-specific features — is **clean, not leaky**. Rationale:

- MEAI's `IChatClient` provides the common chat interface. Provider SDKs provide provider-specific capabilities.
- The `ILLMProvider` adapter already exists in our abstraction layer. Adding MEAI as the underlying transport behind the adapter is an implementation detail.
- Model listing and key validation are inherently provider-specific — no abstraction can hide this.

### Recommendation: **ADOPT WITH CAVEATS**

**Package list:**

| Package | Purpose |
|---------|---------|
| [`Microsoft.Extensions.AI`](https://www.nuget.org/packages/Microsoft.Extensions.AI) (v10.7.0) | Core abstractions, middleware pipeline, DI integration |
| [`Microsoft.Extensions.AI.OpenAI`](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI) (v10.7.0) | OpenAI + all OpenAI-compatible providers (DeepSeek, Mistral, Moonshot, MiMo, local) |
| [`Microsoft.Extensions.AI.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions) (v10.7.0) | Implicit dependency, pulled by above |

**Roadmap Feature:** Feature 12 (Studio Chat Core) — this is where the first LLM HTTP calls happen.

**Integration notes:**

1. **DI Registration Pattern (8 providers):**
```csharp
// OpenAI + all OpenAI-compatible providers share one IChatClient registration
// Provider-specific base URI configured at resolution time
services.AddChatClient(sp => {
    var config = sp.GetRequiredService<ModelConfiguration>();
    var key = sp.GetRequiredService<IApiKeyResolver>().Resolve(config.ProviderType);
    return new OpenAI.Chat.ChatClient(config.ModelId, new ApiKeyCredential(key),
        new OpenAI.Chat.ChatClientOptions { Endpoint = config.EndpointUrl }).AsIChatClient();
}).UseLogging(); // MEAI middleware pipeline
```

2. **Anthropic stays on its own SDK** — do not force through `IChatClient` unless the Anthropic SDK natively implements it. Direct SDK usage for Anthropic-specific features (extended thinking, beta headers).

3. **`FunctionInvokingChatClient` NOT used.** Our `IToolOrchestrator` handles the confirmation/approval/parallel-execution/serial-display workflow that `FunctionInvokingChatClient` cannot.

4. **MEAI middleware** (`.UseLogging()`, `.UseOpenTelemetry()`) used for cross-cutting concerns on OpenAI-compatible providers only.

5. **HSO ecosystem is a non-factor.** Community providers (Gemini, DeepSeek, Mistral, Qwen) route through their existing SDKs or OpenAI-compatible endpoints, not missing NuGet packages.

**What would need to change for no-go:** Stick with current plan (OpenAI SDK + Anthropic SDK + Google SDK directly). No architectural change needed — MEAI is additive middleware, not a replacement.

---

## Item 2 — Polly for HTTP Resilience

### Finding

| Metric | Value |
|--------|-------|
| Package | [`Polly.Core`](https://www.nuget.org/packages/Polly.Core) v8.7.0 |
| Total Downloads | **530M+** |
| .NET 8 Compatible | ✅ Yes (net8.0 target) |
| Last Updated | June 10, 2026 (2 weeks ago) |
| License | BSD-3-Clause |
| Maintainer | App vNext |

[`Microsoft.Extensions.Http.Resilience`](https://www.nuget.org/packages/Microsoft.Extensions.Http.Resilience) provides `AddResilienceHandler()` extension on `IHttpClientBuilder`. Polly.Core v8.x uses `ResiliencePipelineBuilder` with fluent API for retry (exponential backoff + jitter), circuit breaker, timeout, rate limiter.

#### 2a. MEAI Integration

**Finding: Polly does NOT integrate through MEAI's `ChatClientBuilder`.** MEAI's middleware pipeline (`UseDistributedCache`, `UseOpenTelemetry`, `UseLogging`) does not include a `UseResilience()` extension. Resilience must be applied at the `HttpClient` level via `IHttpClientFactory` + `AddResilienceHandler()`, which wraps the HTTP transport beneath MEAI's `IChatClient`. This is the correct layering — HTTP resilience belongs at the transport layer, not the AI abstraction layer.

#### 2b. Recommended Pattern for 8 Providers

```csharp
// Per-provider named HttpClient with resilience pipeline
services.AddHttpClient("OpenAI")
    .AddResilienceHandler("openai-pipeline", builder =>
    {
        builder.AddRetry(new()
        {
            ShouldHandle = args => args.Outcome switch
            {
                { Exception: HttpRequestException } => PredicateResult.True(),
                { Exception: TaskCanceledException } => PredicateResult.True(),
                { Result.StatusCode: >= HttpStatusCode.InternalServerError } => PredicateResult.True(),
                { Result.StatusCode: HttpStatusCode.TooManyRequests } => PredicateResult.True(),
                _ => PredicateResult.False()
            },
            MaxRetryAttempts = 4,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromSeconds(1),
            UseJitter = true,
            MaxDelay = TimeSpan.FromSeconds(30)
        })
        .AddCircuitBreaker(new()
        {
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(30)
        })
        .AddTimeout(TimeSpan.FromMinutes(5)); // LLM calls can be long
    });
```

#### 2c. Provider-Specific Considerations

- **Anthropic `Retry-After` header on 429s:** Polly's `AddRetry` with `ShouldHandle` checking `HttpStatusCode.TooManyRequests` covers this. For optimal behavior, add a custom `RetryStrategyOptions.ShouldHandle` that reads `Retry-After` header and adjusts delay dynamically.
- **OpenAI rate limits:** Same pattern. OpenAI returns `x-ratelimit-reset-requests` headers.
- **Google Gemini:** Uses standard HTTP 429 with optional `Retry-After`.

### Recommendation: **ADOPT**

**Package list:**

| Package | Purpose |
|---------|---------|
| [`Polly.Core`](https://www.nuget.org/packages/Polly.Core) (v8.7.0) | Core resilience strategies |
| [`Microsoft.Extensions.Http.Resilience`](https://www.nuget.org/packages/Microsoft.Extensions.Http.Resilience) (v9.0.0+) | `AddResilienceHandler` DI integration with `IHttpClientFactory` |

**Roadmap Feature:** Feature 12 (Studio Chat Core) — resilience is foundational to all provider HTTP calls.

**Integration notes:**

1. Apply at `HttpClient` layer, not MEAI layer. Each provider gets a named `HttpClient` with its own resilience pipeline.
2. Circuit breaker per-provider (one provider down shouldn't break others).
3. LLM calls need longer timeouts (5 min) than search/model-listing calls (30 sec).
4. 429 responses should be retried with exponential backoff. Consider provider-specific `Retry-After` header parsing.

---

## Item 3 — Dragablz for Draggable/Tearable Tabs

### Finding

| Metric | Value |
|--------|-------|
| Package | [`Dragablz`](https://www.nuget.org/packages/Dragablz) v0.0.3.234 |
| Total Downloads | **1.3M** |
| .NET 8 Compatible | ❌ **NOT explicitly compatible** — targets .NET Core 3.0 and .NET Framework 4.0 only |
| Last Updated | **September 9, 2022** (nearly 4 years ago) |
| License | MIT (repo LICENSE file) |
| GitHub Stars | N/A (repo is public, ~1K indirect via dependents) |

**Critical finding:** Dragablz is effectively unmaintained. Last release was 2022. It targets .NET Core 3.0 — while it may work on .NET 8 through compatibility, there's no guarantee. The library has not been updated for .NET 5, 6, 7, 8, or 9.

#### 3a. WPF-UI Integration

**Finding: No explicit integration.** Dragablz and WPF-UI are independent libraries. Dragablz styles tabs with its own templates; WPF-UI has its own `TabControl` style. Combining them would require custom template work — no known community integration exists.

#### 3b. Known Limitations

- No .NET 8 targeting — compatibility is untested
- Last commit activity was ~2022
- MVVM support exists but is Caliburn.Micro-oriented; CommunityToolkit.Mvvm integration is undocumented
- Tearable-to-floating-window works on .NET Framework but may have issues on .NET 8 with new windowing APIs
- No high-DPI or PerMonitorV2 awareness tested

### Recommendation: **SKIP**

**Rationale:** Unmaintained for 4 years, no .NET 8 target, high integration risk. Building custom tab drag-drop with WPF's built-in `TabControl` + adorners/`DragDrop` events is lower risk and yields better control.

**What changes:** Feature 5 roadmap mentions "tabbed navigation with drag-drop reorder." Instead of Dragablz, implement:
- **Drag-drop tab reorder:** WPF `TabControl` with `AllowDrop="True"` + `PreviewMouseMove` + drag adorner. ~200 lines.
- **Tearable tabs to floating windows:** Custom `Window` creation from detached `TabItem` content. ~300 lines.
- **Tab close/reopen:** WPF standard `TabItem` behavior. ~100 lines.

Custom implementation is estimated at ~600 lines total — manageable and avoids abandoned-dependency risk.

---

## Item 4 — FluentValidation for Form Validation

### Finding

| Metric | Value |
|--------|-------|
| Package | [`FluentValidation`](https://www.nuget.org/packages/FluentValidation) v12.1.1 |
| Total Downloads | **963M+** |
| .NET 8 Compatible | ✅ Yes (net8.0 target) |
| Last Updated | December 3, 2025 |
| License | Apache-2.0 |
| Maintainer | Jeremy Skinner |

#### 4a. CommunityToolkit.Mvvm + INotifyDataErrorInfo Pattern

**Finding: Confirmed works in WPF .NET 8.** `CommunityToolkit.Mvvm`'s `ObservableValidator` implements `INotifyDataErrorInfo` and integrates with FluentValidation via the `Validator` property. WPF controls natively support `INotifyDataErrorInfo` with error templates through `Validation.ErrorTemplate`. The pattern:

```csharp
// ViewModel
public partial class ApiKeyViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    private string _displayName = "";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    private string _apiKey = "";

    public ApiKeyViewModel()
    {
        // FluentValidation for cross-property rules
        Validator = new ApiKeyValidator();
    }
}

// Validator
public class ApiKeyValidator : AbstractValidator<ApiKeyViewModel>
{
    public ApiKeyValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MinimumLength(2);
        RuleFor(x => x.ApiKey).NotEmpty().Must(BeValidFormat)
            .WithMessage("API key format is invalid for the selected provider");
    }
}
```

#### 4b. Conflict with Existing CommunityToolkit.Mvvm Usage

**Finding: No conflict.** `ObservableValidator` is an extension of `ObservableObject` (which existing ViewModels already inherit). Existing ViewModels can optionally add validation without breaking changes.

### Recommendation: **ADOPT**

**Package:** [`FluentValidation`](https://www.nuget.org/packages/FluentValidation) v12.1.1

**Roadmap Feature:** Feature 7 (Model Configurations, API Keys & Personas) — retrofit validation to existing Settings forms; also Feature 8 (Onboarding Wizard), Feature 13 (Prompt Library).

**Integration notes:**

1. Use `ObservableValidator` as base for Settings ViewModels that need validation.
2. Create validator classes for: `ApiKey`, `ModelConfiguration`, `Persona`, `TextAction`, `PromptTemplate`, `OnboardingStep`.
3. WPF `TextBox` styles automatically show `Validation.ErrorTemplate` (red border) when `INotifyDataErrorInfo` reports errors.
4. Submit buttons bind `IsEnabled` to `!HasErrors`.
5. FluentValidation's `DependencyInjectionExtensions` package not needed — we use `ObservableValidator` pattern instead.

---

## Item 5 — Toast Notifications Library

### Finding

**WPF-UI Snackbar** (if Item 7 is adopted):

| Capability | Verdict |
|-----------|---------|
| Multi-toast | ✅ Yes — `SnackbarService` manages a presenter, can queue multiple |
| Themed (Dark/Light) | ✅ Yes — `ControlAppearance` enum (Primary, Secondary, Success, Danger, Caution, Dark, Light) |
| MVVM-friendly | ✅ Yes — `ISnackbarService` injectable via DI, `Show(title, message, appearance, icon, timeout)` |
| Positioned bottom-right | ✅ Yes — `SnackbarPresenter` placed in XAML at desired location |
| Icon support | ✅ Yes — `IconElement` parameter |

**Standalone `ToastNotifications` (MIT):**

| Metric | Value |
|--------|-------|
| NuGet | `Wpf.Notifications` / `ToastNotifications` |
| Maintenance | Moderate — less active than WPF-UI |
| Feature set | Basic toast with position, animation, expiration |

**Finding:** If WPF-UI is adopted (Item 7 recommendation), its built-in `Snackbar`/`ISnackbarService` covers all our toast notification scenarios with zero additional dependency. Standalone libraries add dependency overhead for marginal benefit.

### Recommendation: **ADOPT WPF-UI Snackbar** (contingent on Item 7 adoption)

If Item 7 is skipped, adopt standalone [`Wpf.Notifications`](https://www.nuget.org/packages/Wpf.Notifications) or build a simple custom toast (~200 lines with WPF `Popup` + storyboard animations).

**Roadmap Feature:** Feature 12 (Studio Chat Core) — toasts for streaming completion, copy-to-clipboard, errors; Feature 23 (Micro-interactions) — toast slide-in/out animations.

---

## Item 6 — Refit for Typed HTTP Clients

### Finding

| Metric | Value |
|--------|-------|
| Package | [`Refit`](https://www.nuget.org/packages/Refit) v12.0.0 |
| Total Downloads | **176M+** |
| .NET 8 Compatible | ✅ Yes (net8.0 target) |
| Last Updated | June 24, 2026 (3 days ago!) |
| License | MIT |
| Maintainer | ReactiveUI |

#### 6a. Value for Our Use Cases

| Use Case | Refit Value | Raw HttpClient |
|-----------|-------------|----------------|
| Google/Bing Search API (2 endpoints) | Low — 2 simple GET endpoints with query params | Trivial |
| Provider model-listing (8 providers) | Low — each provider has 1-2 endpoints | Trivial |
| ValidateKey calls (8 providers) | Low — simple GET/POST | Trivial |
| OpenAI API (chat, streaming, models) | None — already handled by OpenAI SDK | N/A |
| Anthropic API | None — already handled by Anthropic SDK | N/A |

**Finding: Refit adds marginal value for our use cases.** Total HTTP surface area is ~15-20 simple endpoints. The source generation and interface-based typing are nice but don't solve a meaningful pain point — our endpoints are too simple to benefit meaningfully.

#### 6b. Polly Pairing

Refit + Polly integration is excellent via `AddRefitClient<T>().AddResilienceHandler()`. However, we can achieve the same with named `HttpClient` + `AddResilienceHandler()` without Refit.

### Recommendation: **SKIP — use HttpClient directly**

**Rationale:** Our HTTP surface is too small (~15-20 simple REST endpoints across search APIs + model listing + key validation) to justify Refit's abstraction overhead. `HttpClient` + `System.Text.Json` is sufficient. Polly resilience is applied directly via `AddResilienceHandler()` on named `HttpClient` instances.

**What changes:** Keep `ISearchProvider` and `ILLMProvider.ListModelsAsync()` / `ValidateKeyAsync()` using injected `HttpClient` (or provider SDKs where available). No additional library needed.

---

## Item 7 — WPF-UI Adoption

### Finding

| Metric | Value |
|--------|-------|
| Package | [`WPF-UI`](https://www.nuget.org/packages/WPF-UI) v4.3.0 |
| Total Downloads | **941K** |
| .NET 8 Compatible | ✅ Yes (net8.0-windows target) |
| Last Updated | May 4, 2026 (active) |
| License | MIT |
| Maintainer | Leszek Pomianowski (lepo.co) |
| GitHub Stars | lepoco/wpfui: **high profile** in WPF ecosystem |

**Production users:** EverythingToolbar (14.3K ★), StabilityMatrix (8.4K ★), LenovoLegionToolkit (7.5K ★), Text-Grab (4.8K ★), FluentFlyout (3.3K ★), Cosmos OS (3.2K ★).

#### 7a. WPF-UI vs Material Design In XAML

| Aspect | WPF-UI | Material Design In XAML |
|--------|--------|------------------------|
| Design language | Windows 11 Fluent Design | Google Material Design |
| Native Windows feel | ✅ Excellent — matches OS | ❌ Feels like Android on Windows |
| Controls | Navigation, NumberBox, Dialog, Snackbar, InfoBar, CardControl | Material-themed standard controls |
| Theme engine | `ApplicationThemeManager` — Dark/Light/HighContrast | `ThemeAssist` — more complex |
| Windows 11 features | Mica/Acrylic backdrops, SnapLayout | Not available |

**Conclusion:** For a Windows-exclusive WPF app, WPF-UI is the clear winner. Material Design in XAML produces a non-Windows-native look.

#### 7b. Dragablz Integration

**Finding: Not applicable** — Dragablz was recommended as SKIP (Item 3). WPF-UI provides its own `TabControl` style with Fluent appearance. Custom drag-drop can be built on top.

#### 7c. Theme Migration Path

**Finding: `ApplicationThemeManager` CAN replace manual `DynamicResource` swapping, but requires migration effort.** Current approach: `Dark.xaml` / `Light.xaml` ResourceDictionaries with `DynamicResource` references, manually swapped via `WpfThemeProvider`. WPF-UI's `ApplicationThemeManager.Apply(ApplicationTheme.Dark/Light)` provides the same capability with additional benefits:
- System theme auto-detection and following
- High Contrast theme support
- Mica/Acrylic backdrop
- Automatic accent color from Windows settings
- Built-in control templates themed automatically

**Migration impact:** Moderate. Existing `DynamicResource` keys must be mapped to WPF-UI's resource keys, OR custom brushes can coexist with WPF-UI's theme. Not a full rewrite — the existing `Dark.xaml`/`Light.xaml` dictionaries can be retained and augmented with WPF-UI's resources.

### Recommendation: **ADOPT — deploy in Feature 5b (Visual Design System)**

**Roadmap Feature:** Feature 5b (Visual Design System: Colors, Typography & Spacing)

**Integration notes:**

1. **Before Feature 12:** Add WPF-UI NuGet, set up `ApplicationThemeManager`, verify existing screens render correctly with WPF-UI's default styles.
2. **Theme migration path:**
   - Keep existing `Dark.xaml`/`Light.xaml` for custom resources
   - Add WPF-UI's `ApplicationThemeManager.Apply()` in `App.xaml.cs` startup
   - WPF-UI's implicit styles will override default WPF control templates with Fluent versions
   - Custom `DynamicResource` keys remain functional — WPF merges dictionaries
3. **Snackbar:** Use `ISnackbarService` + `SnackbarPresenter` in `MainWindow.xaml`
4. **Icon story:** Use WPF-UI's built-in `SymbolIcon` (Fluent System Icons) for UI chrome; custom SVG via SharpVectors (Item 8) for specialized icons
5. **Risk:** Existing custom control templates (if any) may conflict with WPF-UI's implicit styles. Mitigate with explicit `x:Key` on custom templates. Audit existing XAML before migration.

**Alternative considered:** Defer to Feature 22 (UI Polish). Rejected — adopting WPF-UI before building Wave 3 vertical slices avoids re-styling every screen later.

---

## Item 8 — SharpVectors for SVG Icons

### Finding

| Metric | Value |
|--------|-------|
| Package | [`SharpVectors`](https://www.nuget.org/packages/SharpVectors) v1.8.5 |
| Total Downloads | **2.1M** |
| .NET 8 Compatible | ✅ Yes (net8.0-windows target) |
| Last Updated | August 13, 2025 (active) |
| License | BSD-3-Clause |
| Maintainer | Elinam LLC |

SharpVectors provides `SvgViewbox` XAML control for inline SVG rendering at native WPF resolution — automatically DPI-aware. Also includes SVG-to-XAML conversion for build-time optimization.

#### 8b. Icon Set Recommendation

| Icon Set | License | Style | Recommendation |
|----------|---------|-------|----------------|
| **Fluent UI System Icons** (Microsoft) | MIT | Windows 11 native | ✅ **Best fit** — ships with WPF-UI's `SymbolIcon`, 2K+ icons |
| Phosphor Icons | MIT | Clean, versatile | Good alternative if Fluent icons insufficient |
| Segoe Fluent Icons (Windows) | Built into Windows | Segoe MDL2 | Free but limited; no SVG distribution |

**Recommendation: Use WPF-UI's built-in `SymbolIcon` (Fluent UI System Icons) for standard UI chrome** (navigation, actions, status). **Use SharpVectors `SvgViewbox` for specialized icons** (provider logos, custom tool icons, brand assets) where Fluent icons don't have equivalents.

#### 8c. WPF-UI Built-in Icon Support

WPF-UI ships `SymbolIcon` control with `SymbolRegular` enum (Fluent UI System Icons). This covers toolbar icons, navigation icons, and status indicators without additional dependencies.

### Recommendation: **ADOPT WITH CAVEATS**

**Package:** [`SharpVectors`](https://www.nuget.org/packages/SharpVectors) v1.8.5 — only for custom SVG assets (provider logos, tool icons not in Fluent set)

**Roadmap Feature:** Feature 5b (Visual Design System) — icon strategy alongside WPF-UI adoption.

**Integration notes:**

1. **Default approach:** Use WPF-UI `SymbolIcon` for all standard UI icons (navigation, toolbar, status). No SharpVectors needed for these.
2. **SharpVectors usage:** Only for provider logos, custom tool icons, and AI-generated SVG content. Use `SvgViewbox` XAML control directly.
3. **Build-time optimization:** For frequently-used custom SVGs, use SharpVectors' SVG-to-XAML converter to avoid runtime parsing overhead.
4. **DPI:** `SvgViewbox` is inherently DPI-aware (renders to WPF vector primitives).

---

## Item 9 — XamlFlair for Micro-Interaction Animations

### Finding

| Metric | Value |
|--------|-------|
| Package | [`XamlFlair.WPF`](https://www.nuget.org/packages/XamlFlair.WPF) v1.2.13 |
| Total Downloads | **61K only** |
| .NET 8 Compatible | ❌ **NOT compatible** — targets .NET Core 3.0/3.1, .NET Framework 4.6.1+ |
| Last Updated | **October 3, 2021** (nearly 5 years ago) |
| License | Unknown (repo: XamlFlair/XamlFlair) |
| GitHub Stars | ~90 across repos |

**Critical finding:** XamlFlair.WPF is essentially abandoned. Last release was 2021. No .NET 5/6/7/8 targeting. Only 61K total downloads — niche adoption. The library provides XAML-based animation settings (Fade, Translate, Scale, Rotate) via attached properties and `AnimationSettings` resources — functionality that WPF natively supports through `Storyboard` and `DoubleAnimation`.

#### 9b. SPI_GETCLIENTAREAANIMATION Accessibility

**Finding: No accessibility support in XamlFlair.** No indication of Windows animation-setting awareness (`SystemParametersInfo` with `SPI_GETCLIENTAREAANIMATION`). This would need to be custom-built regardless.

#### 9c. Fit for Our Animation Requirements

| Requirement | XamlFlair | WPF Native |
|-------------|-----------|------------|
| Hover transitions (150ms color fade) | ✅ `Fade` animation | ✅ `Storyboard` + `ColorAnimation` |
| Panel resize animations | ⚠️ Limited | ✅ `Storyboard` + `DoubleAnimation` on `GridLength` |
| Tab transitions | ⚠️ No specific support | ✅ Custom `TabControl` template animations |
| Message fade-in | ✅ `FadeFrom` | ✅ `Storyboard` + `DoubleAnimation` on `Opacity` |
| Toast slide-in/out | ✅ `TranslateXFrom` | ✅ `Storyboard` + `ThicknessAnimation` |
| Thinking expand/collapse | ⚠️ No specific support | ✅ `Storyboard` on `Expander` |
| Accessibility disable | ❌ Not supported | ✅ Custom check before animation |

### Recommendation: **SKIP — build animations with native WPF Storyboards**

**Rationale:** XamlFlair is abandoned (5 years without updates), not .NET 8 compatible, and provides marginal convenience over WPF's built-in `Storyboard`/`DoubleAnimation`/`ThicknessAnimation` system. The animation surface in Feature 23 is modest — ~10 animation types — and can be encapsulated in reusable WPF `Style` triggers and attached behaviors.

**What changes:** Feature 23 (Micro-interactions & Motion Design) implements animations using:
1. **Reusable `Style` triggers** in `AnimationStyles.xaml` ResourceDictionary — defines hover, fade-in, slide-in keyframe animations
2. **`AnimationBehavior` attached property** — enables/disable animations per-element based on `SPI_GETCLIENTAREAANIMATION`
3. **Custom markup extension** — `{StaticAnimation FadeIn}` resolves to a pre-built `Storyboard`

This approach is ~400 lines of infrastructure code, 100% .NET 8 native, and fully accessibility-aware.

---

## Additional Item A — ChatGptNet (marcominerva/ChatGptNet)

**Evaluated per user request:** https://github.com/marcominerva/chatgptnet

### Finding

| Metric | Value |
|--------|-------|
| Package | [`ChatGptNet`](https://www.nuget.org/packages/ChatGptNet) v3.3.9 |
| Total Downloads | 33.4K |
| .NET 8 Compatible | ✅ Yes (net8.0 target) |
| Last Updated | November 6, 2024 |
| GitHub | **ARCHIVED** on Feb 24, 2026 |
| NuGet Status | **DEPRECATED** — "legacy, no longer maintained" |
| Stars | 311 |
| Scope | OpenAI + Azure OpenAI only |

**Critical finding: This library is both archived on GitHub AND deprecated on NuGet.** The author explicitly recommends [`Microsoft.Agents.AI`](https://www.nuget.org/packages/Microsoft.Agents.AI/) as the replacement. It covers only OpenAI and Azure OpenAI — no Anthropic, Google, or other providers. It has meaningful functionality (conversation caching, streaming, tool/function calling, embeddings) but these are all covered by MEAI + official OpenAI SDK.

### Recommendation: **SKIP**

**Rationale:** Archived, deprecated, OpenAI-only. MEAI + official OpenAI SDK provides everything ChatGptNet offered with the benefit of Microsoft support and multi-provider future-proofing.

---

## Additional Item B — MdXaml (whistyun/MdXaml)

**Evaluated per user request:** https://github.com/whistyun/MdXaml

### Finding

| Metric | Value |
|--------|-------|
| Package | [`MdXaml`](https://www.nuget.org/packages/MdXaml) v1.27.0 |
| Total Downloads | 425K |
| .NET 8 Compatible | ⚠️ Targets net6.0-windows, netcoreapp3.0 (NOT net8.0 explicitly) |
| Latest Stable | February 6, 2024 |
| Pre-release | v2.0.0-pre202603081301 (March 8, 2026 — **actively developed!**) |
| License | MIT |
| GitHub Stars | 330 |
| Used By | Shadowsocks-windows (59.6K ★), Flow.Launcher (15K ★), StabilityMatrix (8.4K ★) |

**MdXaml** converts Markdown to WPF `FlowDocument` directly — exactly what our custom [`MarkdownToFlowDocumentConverter`](src/MySecondBrain.UI/Converters/MarkdownToFlowDocumentConverter.cs) does. It provides:
- `Markdown` engine class: `FlowDocument document = engine.Transform(markdownTxt);`
- `MarkdownScrollViewer` XAML control for drop-in Markdown rendering
- AvalonEdit-based syntax highlighting (same as our current approach)
- Plugins for SVG, HTML, animated GIF
- Table support (rowspan, colspan), text alignment, color, strikethrough

#### Fit Analysis

| Requirement | MdXaml | Custom (Current) |
|-------------|--------|------------------|
| Streaming token-by-token rendering | ❌ No streaming API — batch `Transform()` only | ✅ Custom progressive renderer |
| Custom content blocks (thinking, tool cards, citations) | ⚠️ Plugins exist but not for these types | ✅ `IContentBlockRenderer` registry |
| Tool call renderer cards | ❌ No built-in support | ✅ `ToolCallRenderer` (priority 700) |
| Citation rendering with footnotes | ❌ No built-in support | ✅ `CitationRenderer` (priority 350) |
| RTL/BiDi text | ⚠️ FlowDocument inherits WPF BiDi but no explicit RTL detection | ✅ `BidiHelper` with per-segment direction |
| Wiki Browser rendering | ✅ Excellent fit — static Markdown to FlowDocument | ✅ Already working with custom converter |
| Rendering quality | ✅ High — used by Shadowsocks, Flow.Launcher | ✅ Good, but MdXaml may be more polished |
| Syntax highlighting | ✅ AvalonEdit-based (same engine) | ✅ AvalonEdit-based |

**Finding: MdXaml is a strong alternative for static Markdown rendering** (Wiki Browser, Artifact viewer, static message display) but **cannot replace the streaming chat renderer**. Our custom [`MarkdownToFlowDocumentConverter`](src/MySecondBrain.UI/Converters/MarkdownToFlowDocumentConverter.cs) with progressive streaming, custom content block renderers, and RTL/BiDi support is essential for the chat UX.

### Recommendation: **ADOPT WITH CAVEATS — for Wiki Browser only**

**Package:** [`MdXaml`](https://www.nuget.org/packages/MdXaml) v1.27.0 (or v2.0.0 when stable)

**Roadmap Feature:** Feature 19 (Personal Wiki / Second Brain) — use MdXaml for Wiki Browser's Markdown viewer instead of custom FlowDocument conversion.

**Integration notes:**

1. **Wiki Browser ONLY.** Keep custom streaming renderer for chat messages. MdXaml handles static, already-complete Markdown — perfect for wiki file viewing.
2. **Replace the Wiki Browser's custom Markdown-to-FlowDocument converter** with MdXaml's `MarkdownScrollViewer` or `engine.Transform()`.
3. **Benefits:**
   - Better table rendering (rowspan/colspan) than our current converter
   - Free SVG rendering in wiki files via MdXaml.Svg plugin
   - Syntax highlighting via same AvalonEdit engine we already use
   - Reduces custom rendering code by ~300 lines
4. **v2.0.0 pre-release** (March 2026) shows active development — wait for stable v2 release or use v1.27.0.
5. **Risk:** Not explicitly targeting net8.0 (targets net6.0-windows). May work via compatibility but untested. The v2 pre-release may add net8.0 targeting.

---

## Updated Technology Sourcing Summary Table

| # | Item | Final Recommendation | Package(s) | Roadmap Feature | Risk |
|---|------|---------------------|------------|-----------------|------|
| 1 | **MEAI** (Microsoft.Extensions.AI) | **Adopt with caveats** — use for OpenAI-compatible providers via `IChatClient` + middleware. NOT for Anthropic (use native SDK). NOT for `FunctionInvokingChatClient` (use custom `IToolOrchestrator`). | `Microsoft.Extensions.AI` v10.7.0, `Microsoft.Extensions.AI.OpenAI` v10.7.0 | F12 (Studio Chat Core) | Low-Med |
| 2 | **Polly** for HTTP resilience | **Adopt** — per-provider `HttpClient` resilience pipeline with retry + circuit breaker + timeout | `Polly.Core` v8.7.0, `Microsoft.Extensions.Http.Resilience` v9.x | F12 (Studio Chat Core) | Low |
| 3 | **Dragablz** for draggable tabs | **SKIP** — unmaintained (2022), no .NET 8 target. Build custom tab drag-drop (~600 lines) | None | F5 (App Shell) — custom implementation | Low |
| 4 | **FluentValidation** for form validation | **Adopt** — `ObservableValidator` + `INotifyDataErrorInfo` pattern | `FluentValidation` v12.1.1 | F7 (retrofit), F8, F13 | Low |
| 5 | **Toast notifications** | **Adopt WPF-UI Snackbar** (contingent on Item 7) | `WPF-UI` v4.3.0 (`ISnackbarService`) | F12, F23 | Low |
| 6 | **Refit** for typed HTTP clients | **SKIP** — HTTP surface too small; use `HttpClient` directly | None | N/A | N/A |
| 7 | **WPF-UI** adoption | **Adopt** — deploy in Feature 5b before Wave 3 vertical slices | `WPF-UI` v4.3.0 | F5b (Visual Design System) | Medium — migration impact on existing theming |
| 8 | **SharpVectors** for SVG icons | **Adopt with caveats** — only for custom SVGs; use WPF-UI `SymbolIcon` for standard icons | `SharpVectors` v1.8.5 | F5b | Low |
| 9 | **XamlFlair** for animations | **SKIP** — abandoned (2021), no .NET 8 target. Build with native WPF Storyboards | None | F23 (Micro-interactions) — custom implementation | Low |
| A | **ChatGptNet** | **SKIP** — archived + deprecated, OpenAI-only | None | N/A | N/A |
| B | **MdXaml** for Markdown rendering | **Adopt with caveats** — Wiki Browser only; keep custom streaming renderer for chat | `MdXaml` v1.27.0 (or v2.0.0) | F19 (Personal Wiki) | Low-Med — not explicitly net8.0 |

### Categorical Distribution (Updated)

| Category | Count | Items |
|----------|-------|-------|
| **Adopt** | 3 | Polly, FluentValidation, WPF-UI |
| **Adopt with caveats** | 4 | MEAI, Toast (via WPF-UI), SharpVectors, MdXaml |
| **SKIP** | 4 | Dragablz, Refit, XamlFlair, ChatGptNet |

### Affected Roadmap Features

| Feature | Technology Decision Impact |
|---------|---------------------------|
| **F5** (App Shell) | Dragablz removed; custom tab drag-drop built instead |
| **F5b** (Visual Design System) | **WPF-UI adopted** here — theme infrastructure, icon strategy (WPF-UI `SymbolIcon` + SharpVectors for custom SVGs) |
| **F7** (Model Configs, API Keys, Personas) | FluentValidation retrofit for form validation |
| **F12** (Studio Chat Core) | MEAI for OpenAI-compatible providers, Polly for resilience, WPF-UI Snackbar for toasts |
| **F19** (Personal Wiki) | MdXaml for Wiki Browser static Markdown rendering |
| **F23** (Micro-interactions) | XamlFlair replaced by native WPF Storyboard animations with accessibility awareness |

### Dependency Chain Impact

```
F5 (no Dragablz → custom tab implementation)
 └── F5b ← WPF-UI + SharpVectors + icon strategy
      └── F12 ← MEAI + Polly + WPF-UI Snackbar
           └── F7 ← FluentValidation retrofit
           └── F19 ← MdXaml for Wiki Browser
           └── F23 ← Native WPF animations (no XamlFlair)
```

---

*Investigation completed June 27, 2026. All findings based on NuGet.org data, GitHub repository analysis, and Context7 documentation as of this date. Community packages (HSO.Extensions.AI) could not be verified to exist — the provider ecosystem around MEAI for non-OpenAI/non-Ollama providers is thinner than the original sourcing brief suggested.*
