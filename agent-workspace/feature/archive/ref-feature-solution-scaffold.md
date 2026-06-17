# Feature Reference: .NET 8.0 WPF Solution Scaffold

## Global & Shared Documentation

### Project Architecture Documents (Read-Only Reference)
These documents define the architecture that the scaffold must support. They are NOT modified by this feature.

| Document | Path | Relevance to This Feature |
|----------|------|--------------------------|
| Architecture | [`agent-workspace/project-director/planning/architecture.md`](../project-director/planning/architecture.md) | Defines 6 component groups, 7 projects, dependency direction, deployment model |
| Tech Stack | [`agent-workspace/project-director/planning/tech-stack.md`](../project-director/planning/tech-stack.md) | 15 OSS NuGet packages with sourcing rationale and sizing |
| Abstractions | [`agent-workspace/project-director/planning/abstractions.md`](../project-director/planning/abstractions.md) | 12 service interfaces, 7 repository interfaces, 15 platform service interfaces |
| Data Model | [`agent-workspace/project-director/planning/data-model.md`](../project-director/planning/data-model.md) | 13 entities with attributes, relationships, FK dependencies |
| Platform Notes | [`agent-workspace/project-director/planning/platform-notes.md`](../project-director/planning/platform-notes.md) | WPF conventions, DI lifetimes, window management, MSIX capabilities, app.manifest |
| Integration Points | [`agent-workspace/project-director/planning/integration-points.md`](../project-director/planning/integration-points.md) | 23 integration points: abstractions, fallbacks, configuration |
| Planning Summary | [`agent-workspace/project-director/planning/planning-summary.md`](../project-director/planning/planning-summary.md) | Index of all planning docs, architecture decision log, risk heatmap |

### .NET 8.0 WPF Project Type Reference

| Project Type | SDK | TargetFramework | Key Properties |
|-------------|-----|----------------|----------------|
| Class Library | `Microsoft.NET.Sdk` | `net8.0` | — |
| WPF Application | `Microsoft.NET.Sdk` | `net8.0-windows10.0.17763.0` | `<UseWPF>true</UseWPF>`, `<UseWindowsForms>true</UseWindowsForms>` |
| xUnit Test | `Microsoft.NET.Sdk` | `net8.0` | `<IsPackable>false</IsPackable>`, `<IsTestProject>true</IsTestProject>` |
| MSIX Package | `Microsoft.WindowsAppSDK.Sdk` (via .wapproj) | `net8.0-windows10.0.17763.0` | References WPF app project |

### MSIX Capabilities Reference

From [`platform-notes.md` §9](../project-director/planning/platform-notes.md#9-msix-packaging--deployment):

| Capability | Namespace | Purpose |
|-----------|-----------|---------|
| `internetClient` | Standard | LLM API calls, GCS backup, web search, auto-update check |
| `runFullTrust` | `rescap` | P/Invoke (global hotkeys, HWND capture, text injection), file system access |
| `localSystemServices` | `rescap` | `System.Diagnostics.Process` for terminal tool execution |

`rescap` namespace: `xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"`

### Directory.Build.props Common Settings

All 7 projects inherit these settings. The file lives at repo root.

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn> <!-- Missing XML comment for publicly visible type -->
  </PropertyGroup>
</Project>
```

**Note:** The UI project overrides `TargetFramework` to `net8.0-windows10.0.17763.0` in its own `.csproj`.

### Solution-Wide .editorconfig Rules

```ini
root = true

[*.cs]
# Style
dotnet_style_prefer_var_when_type_is_obvious = true:warning
dotnet_style_prefer_var_otherwise = true:suggestion
dotnet_style_qualification_for_field = false:warning
dotnet_style_qualification_for_property = false:warning
dotnet_style_qualification_for_method = false:warning
dotnet_style_qualification_for_event = false:warning
csharp_style_var_for_built_in_types = true:warning
csharp_style_var_when_type_is_apparent = true:warning
csharp_style_var_elsewhere = true:suggestion

# Usings
csharp_using_directive_placement = outside_namespace:warning
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = true
dotnet_style_namespace_match_folder = true:warning

# Modifiers
csharp_prefer_static_local_function = true:warning
csharp_preferred_modifier_order = public,private,protected,internal,static,extern,new,virtual,abstract,sealed,override,readonly,unsafe,volatile,async:warning
dotnet_style_readonly_field = true:warning

# Patterns
csharp_style_pattern_matching_over_as_with_null_check = true:warning
csharp_style_pattern_matching_over_is_with_cast_check = true:warning
csharp_style_inlined_variable_declaration = true:warning
csharp_style_throw_expression = true:warning
csharp_style_conditional_delegate_call = true:warning

# New() vs new()
csharp_style_implicit_object_creation_when_type_is_apparent = true:warning

# Null checking
csharp_style_throw_expression = true:warning
csharp_style_prefer_null_check_over_type_check = true:warning

# File-scoped namespaces
csharp_style_namespace_declarations = file_scoped:warning

# Indentation
indent_style = space
indent_size = 4
tab_width = 4
```

---

## Step-Specific Documentation

### Step 1: Create Solution File & Solution-Wide Configuration

**External references needed:**
- [.NET 8.0 global.json reference](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) — SDK pinning and roll-forward policy
- [Directory.Build.props](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-your-build) — MSBuild property inheritance
- [.editorconfig for .NET](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/code-style-rule-options) — code style rule options

**Key commands:**
```bash
# Create empty solution (VS 2022 format)
dotnet new sln -n MySecondBrain --format slnx

# Verify
dotnet sln list
dotnet --version
```

**Files to create:**
1. `MySecondBrain.sln` — created via `dotnet new sln`
2. `global.json` — manual write (see template below)
3. `Directory.Build.props` — manual write (see "Global & Shared" above)
4. `.editorconfig` — manual write (see "Global & Shared" above)

**`global.json` template:**
```json
{
  "sdk": {
    "version": "8.0.400",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

> **Note:** `rollForward: latestFeature` allows any 8.0.x SDK patch to be used (e.g., 8.0.403). `allowPrerelease: false` ensures no preview SDKs are used. The version floor `8.0.400` corresponds to the .NET 8.0 LTS baseline.

**`.gitignore` note:** Already exists at repo root. Verify it covers: `bin/`, `obj/`, `*.user`, `*.suo`, `.vs/`, `packages/`, `*.nupkg`, `TestResults/`, `*.trx`, `*.coverage`.

---

### Step 2: Create MySecondBrain.Core Project

**External references needed:**
- No external NuGet packages. Pure .NET 8.0 class library.

**Key commands:**
```bash
# From repo root
mkdir src\MySecondBrain.Core\Interfaces
mkdir src\MySecondBrain.Core\Models
mkdir src\MySecondBrain.Core\Extensions

# Create empty .gitkeep files (Git doesn't track empty dirs)
echo. > src\MySecondBrain.Core\Interfaces\.gitkeep
echo. > src\MySecondBrain.Core\Models\.gitkeep
echo. > src\MySecondBrain.Core\Extensions\.gitkeep

# Add to solution
dotnet sln add src\MySecondBrain.Core\MySecondBrain.Core.csproj

# Verify
dotnet build src\MySecondBrain.Core\MySecondBrain.Core.csproj
```

**`GlobalUsings.cs` content:**
```csharp
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
```

**`.csproj` — no PackageReference elements:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>MySecondBrain.Core</RootNamespace>
    <AssemblyName>MySecondBrain.Core</AssemblyName>
    <Description>Core abstractions, interfaces, DTOs, and enums for MySecondBrain</Description>
  </PropertyGroup>
</Project>
```

**Note on CommunityToolkit.Mvvm:** NOT referenced in Core. The `ObservableObject` base class, `[ObservableProperty]`, and `[RelayCommand]` source generators are used only by ViewModels, which live in the UI project. Core DTOs use plain C# records.

---

### Step 3: Create MySecondBrain.Data Project

**External references needed:**
- [Microsoft.EntityFrameworkCore.Sqlite 8.0 NuGet](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/)
- [Microsoft.Data.Sqlite 8.0 NuGet](https://www.nuget.org/packages/Microsoft.Data.Sqlite/)
- [Microsoft.EntityFrameworkCore.Design 8.0 NuGet](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Design/)

**Key commands:**
```bash
mkdir src\MySecondBrain.Data\Entities
mkdir src\MySecondBrain.Data\Repositories
mkdir src\MySecondBrain.Data\Migrations
mkdir src\MySecondBrain.Data\Configurations

dotnet sln add src\MySecondBrain.Data\MySecondBrain.Data.csproj
dotnet build src\MySecondBrain.Data\MySecondBrain.Data.csproj
```

**`.csproj` structure:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>MySecondBrain.Data</RootNamespace>
    <AssemblyName>MySecondBrain.Data</AssemblyName>
    <Description>Entity Framework Core data layer with SQLite for MySecondBrain</Description>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.*" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MySecondBrain.Core\MySecondBrain.Core.csproj" />
  </ItemGroup>
</Project>
```

**`AppDbContext.cs`** — see plan §5 Step 3 for full content. Key points:
- Constructor accepts `DbContextOptions<AppDbContext>`
- `OnConfiguring` fallback to `UseSqlite("Data Source=msb.db")` if not configured via DI
- Empty `DbSet<>` properties will be added by subsequent features (entities not yet defined)

**Note on EF Core migrations:** The `Migrations/` directory is empty. Migrations will be created by the feature that first defines entity classes (likely Feature 3 or 4). The `Microsoft.EntityFrameworkCore.Design` package is included now so the tooling is available when needed.

---

### Step 4: Create MySecondBrain.Services Project

**External references needed (15 NuGet packages):**

| Package | NuGet URL | Latest Stable (as of planning) |
|---------|-----------|-------------------------------|
| Markdig | https://www.nuget.org/packages/Markdig | Use `*` wildcard |
| OpenAI | https://www.nuget.org/packages/OpenAI | Use `*` wildcard |
| Anthropic.SDK | https://www.nuget.org/packages/Anthropic.SDK | Use `*` wildcard |
| Google.Cloud.AIPlatform.V1 | https://www.nuget.org/packages/Google.Cloud.AIPlatform.V1 | Use `*` wildcard |
| Google.Cloud.Storage.V1 | https://www.nuget.org/packages/Google.Cloud.Storage.V1 | Use `*` wildcard |
| SharpToken | https://www.nuget.org/packages/SharpToken | Use `*` wildcard |
| NAudio | https://www.nuget.org/packages/NAudio | Use `*` wildcard |
| DiffPlex | https://www.nuget.org/packages/DiffPlex | Use `*` wildcard |
| QuestPDF | https://www.nuget.org/packages/QuestPDF | Use `*` wildcard |
| LibGit2Sharp | https://www.nuget.org/packages/LibGit2Sharp | Use `*` wildcard |
| AForge.Video.DirectShow | https://www.nuget.org/packages/AForge.Video.DirectShow | Use `*` wildcard |
| Whisper.net | https://www.nuget.org/packages/Whisper.net | Use `*` wildcard |
| Microsoft.Extensions.DependencyInjection | Built-in / NuGet | `8.0.*` |
| Microsoft.Extensions.Hosting | NuGet | `8.0.*` |
| Microsoft.Extensions.Logging | NuGet | `8.0.*` |

**Key commands:**
```bash
mkdir src\MySecondBrain.Services\Chat
mkdir src\MySecondBrain.Services\LLM
mkdir src\MySecondBrain.Services\Wiki
mkdir src\MySecondBrain.Services\Tools
mkdir src\MySecondBrain.Services\Backup
mkdir src\MySecondBrain.Services\Audio
mkdir src\MySecondBrain.Services\Encryption
mkdir src\MySecondBrain.Services\Update

dotnet sln add src\MySecondBrain.Services\MySecondBrain.Services.csproj

# Restore to verify all packages resolve
dotnet restore src\MySecondBrain.Services\MySecondBrain.Services.csproj
dotnet build src\MySecondBrain.Services\MySecondBrain.Services.csproj
```

**`.csproj` key sections:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>MySecondBrain.Services</RootNamespace>
    <AssemblyName>MySecondBrain.Services</AssemblyName>
    <Description>Business logic services, LLM provider adapters, and integration wrappers</Description>
  </PropertyGroup>

  <ItemGroup>
    <!-- Markdown -->
    <PackageReference Include="Markdig" Version="*" />
    
    <!-- LLM SDKs -->
    <PackageReference Include="OpenAI" Version="*" />
    <PackageReference Include="Anthropic.SDK" Version="*" />
    <PackageReference Include="Google.Cloud.AIPlatform.V1" Version="*" />
    
    <!-- Token Counting -->
    <PackageReference Include="SharpToken" Version="*" />
    
    <!-- Audio -->
    <PackageReference Include="NAudio" Version="*" />
    
    <!-- Diff -->
    <PackageReference Include="DiffPlex" Version="*" />
    
    <!-- PDF -->
    <PackageReference Include="QuestPDF" Version="*" />
    
    <!-- Git -->
    <PackageReference Include="LibGit2Sharp" Version="*" />
    
    <!-- Webcam -->
    <PackageReference Include="AForge.Video.DirectShow" Version="*" />
    
    <!-- Local STT -->
    <PackageReference Include="Whisper.net" Version="*" />
    
    <!-- Backup -->
    <PackageReference Include="Google.Cloud.Storage.V1" Version="*" />
    
    <!-- DI and Hosting -->
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.*" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.*" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MySecondBrain.Core\MySecondBrain.Core.csproj" />
    <ProjectReference Include="..\MySecondBrain.Data\MySecondBrain.Data.csproj" />
  </ItemGroup>
</Project>
```

**Note on `*` version wildcard:** At build time, NuGet resolves `*` to the latest stable version. The Feature Developer must run `dotnet restore` and verify no version conflicts (e.g., two packages depending on incompatible versions of a shared transitive dependency). If conflicts arise, pin specific versions using `[x.y.z]` syntax.

---

### Step 5: Create MySecondBrain.UI WPF Application Project

**External references needed:**
- [CommunityToolkit.Mvvm 8.x NuGet](https://www.nuget.org/packages/CommunityToolkit.Mvvm/) (latest 8.x)
- [LiveCharts2 2.x NuGet](https://www.nuget.org/packages/LiveCharts2/) (WPF package)
- [WeCantSpell.Hunspell 4.x NuGet](https://www.nuget.org/packages/WeCantSpell.Hunspell/)
- [Autoupdater.NET.Official 2.x NuGet](https://www.nuget.org/packages/Autoupdater.NET.Official/)

**Key commands:**
```bash
mkdir src\MySecondBrain.UI\Views
mkdir src\MySecondBrain.UI\ViewModels
mkdir src\MySecondBrain.UI\Controls
mkdir src\MySecondBrain.UI\Themes
mkdir src\MySecondBrain.UI\Converters
mkdir src\MySecondBrain.UI\Services
mkdir src\MySecondBrain.UI\Resources

dotnet sln add src\MySecondBrain.UI\MySecondBrain.UI.csproj
dotnet build src\MySecondBrain.UI\MySecondBrain.UI.csproj
```

**`.csproj` structure:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.17763.0</TargetFramework>
    <RootNamespace>MySecondBrain.UI</RootNamespace>
    <AssemblyName>MySecondBrain.UI</AssemblyName>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationManifest>App.manifest</ApplicationManifest>
    <Description>WPF desktop application for MySecondBrain — three-tier UI</Description>
    <ApplicationIcon>Resources\app.ico</ApplicationIcon>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
    <PackageReference Include="LiveCharts2" Version="2.*" />
    <PackageReference Include="WeCantSpell.Hunspell" Version="4.*" />
    <PackageReference Include="Autoupdater.NET.Official" Version="2.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MySecondBrain.Core\MySecondBrain.Core.csproj" />
    <ProjectReference Include="..\MySecondBrain.Data\MySecondBrain.Data.csproj" />
    <ProjectReference Include="..\MySecondBrain.Services\MySecondBrain.Services.csproj" />
  </ItemGroup>
</Project>
```

**`UseWindowsForms=true` rationale:** Required for `System.Windows.Forms.NotifyIcon` (system tray integration). The architecture calls for WinForms interop specifically for this purpose. No WinForms controls are used in the UI — only `NotifyIcon`.

**`App.xaml` — minimal:**
```xml
<Application x:Class="MySecondBrain.UI.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnMainWindowClose">
    <Application.Resources>
        <!-- Themes and global styles added by subsequent features -->
    </Application.Resources>
</Application>
```

> **Note:** `StartupUri` is intentionally omitted. The `App.xaml.cs` `OnStartup` override creates and shows `MainWindow` via the DI container (see plan §5 Step 5). Setting `StartupUri` would cause WPF to auto-create a second, non-DI `MainWindow` instance.

**`MainWindow.xaml` — minimal:**
```xml
<Window x:Class="MySecondBrain.UI.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="MySecondBrain" Height="900" Width="1400"
        WindowStartupLocation="CenterScreen"
        MinHeight="600" MinWidth="800">
    <Grid>
        <!-- Studio layout added by subsequent features -->
    </Grid>
</Window>
```

**`App.manifest`** — see plan §5 Step 5 for full content. The two critical sections:
1. `<dpiAwareness>PerMonitorV2</dpiAwareness>` — crisp rendering at all DPI scales
2. `<supportedOS>` entries for Windows 10 and Windows 11

---

### Step 6: Create MySecondBrain.Tests.Unit Project

**External references needed:**
- [xUnit 2.x](https://www.nuget.org/packages/xunit/)
- [xunit.runner.visualstudio 2.x](https://www.nuget.org/packages/xunit.runner.visualstudio/)
- [Microsoft.NET.Test.Sdk 17.x](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/)
- [Moq 4.x](https://www.nuget.org/packages/Moq/)
- [coverlet.collector 6.x](https://www.nuget.org/packages/coverlet.collector/)

**Key commands:**
```bash
mkdir tests\unit\MySecondBrain.Tests.Unit

dotnet sln add tests\unit\MySecondBrain.Tests.Unit\MySecondBrain.Tests.Unit.csproj
dotnet build tests\unit\MySecondBrain.Tests.Unit\MySecondBrain.Tests.Unit.csproj
dotnet test tests\unit\MySecondBrain.Tests.Unit\MySecondBrain.Tests.Unit.csproj
```

**`.csproj` structure:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>MySecondBrain.Tests.Unit</RootNamespace>
    <AssemblyName>MySecondBrain.Tests.Unit</AssemblyName>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="Moq" Version="4.*" />
    <PackageReference Include="coverlet.collector" Version="6.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\src\MySecondBrain.Core\MySecondBrain.Core.csproj" />
    <ProjectReference Include="..\..\..\src\MySecondBrain.Data\MySecondBrain.Data.csproj" />
    <ProjectReference Include="..\..\..\src\MySecondBrain.Services\MySecondBrain.Services.csproj" />
    <ProjectReference Include="..\..\..\src\MySecondBrain.UI\MySecondBrain.UI.csproj" />
  </ItemGroup>
</Project>
```

**Note on WPF project reference:** Referencing the UI project from tests is necessary for ViewModel tests. However, WPF types require STA thread. ViewModel tests that don't touch WPF types work fine. For tests that need WPF types, use `[StaFact]` or `[WpfFact]` (xunit.stafact NuGet) — this can be added when the first ViewModel test is written.

---

### Step 7: Create MySecondBrain.Tests.Integration Project

**External references needed:**
- Same testing packages as Step 6 (xUnit, Test.Sdk, coverlet) except Moq (integration tests use real components)

**Key commands:**
```bash
mkdir tests\integration\MySecondBrain.Tests.Integration

dotnet sln add tests\integration\MySecondBrain.Tests.Integration\MySecondBrain.Tests.Integration.csproj
dotnet build tests\integration\MySecondBrain.Tests.Integration\MySecondBrain.Tests.Integration.csproj
dotnet test tests\integration\MySecondBrain.Tests.Integration\MySecondBrain.Tests.Integration.csproj
```

**`.csproj` structure:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>MySecondBrain.Tests.Integration</RootNamespace>
    <AssemblyName>MySecondBrain.Tests.Integration</AssemblyName>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="coverlet.collector" Version="6.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\src\MySecondBrain.Core\MySecondBrain.Core.csproj" />
    <ProjectReference Include="..\..\..\src\MySecondBrain.Data\MySecondBrain.Data.csproj" />
    <ProjectReference Include="..\..\..\src\MySecondBrain.Services\MySecondBrain.Services.csproj" />
    <ProjectReference Include="..\..\..\src\MySecondBrain.UI\MySecondBrain.UI.csproj" />
  </ItemGroup>
</Project>
```

---

### Step 8: Create MySecondBrain.Package MSIX Packaging Project

**External references needed:**
- [Windows Application Packaging Project](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-packaging-dot-net) — MSIX packaging for WPF .NET
- [MSIX Capabilities](https://learn.microsoft.com/en-us/windows/uwp/packaging/app-capability-declarations) — capability reference
- [Restricted Capabilities](https://learn.microsoft.com/en-us/windows/uwp/packaging/app-capability-declarations#restricted-capabilities) — `runFullTrust`, `localSystemServices`

**Key commands:**
```bash
mkdir src\MySecondBrain.Package

# The .wapproj is created manually (no dotnet new template for wapproj without Visual Studio)
# Alternative: use Visual Studio to add the project, or create .wapproj by hand

dotnet sln add src\MySecondBrain.Package\MySecondBrain.Package.wapproj
dotnet build src\MySecondBrain.Package\MySecondBrain.Package.wapproj
```

**`.wapproj` structure:**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" DefaultTargets="Build"
         xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.17763.0</TargetFramework>
    <ProjectGuid>{PUT-NEW-GUID-HERE}</ProjectGuid>
    <EntryPointProjectUniqueName>..\MySecondBrain.UI\MySecondBrain.UI.csproj</EntryPointProjectUniqueName>
    <GenerateAppInstallerFile>False</GenerateAppInstallerFile>
    <AppxPackageSigningEnabled>False</AppxPackageSigningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\MySecondBrain.UI\MySecondBrain.UI.csproj" />
  </ItemGroup>
  <Import Project="$(WapProjPath)\Microsoft.DesktopBridge.targets" />
</Project>
```

**`Package.appxmanifest` — key sections:**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
         xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10">
  <Identity Name="MySecondBrain" Publisher="CN=MySecondBrain" Version="0.1.0.0" />
  <Properties>
    <DisplayName>MySecondBrain</DisplayName>
    <PublisherDisplayName>MySecondBrain</PublisherDisplayName>
  </Properties>
  <Applications>
    <Application Id="MySecondBrain" Executable="MySecondBrain.UI.exe" EntryPoint="Windows.FullTrustApplication">
      <desktop:Extension Category="windows.fullTrustProcess" />
    </Application>
  </Applications>
  <Capabilities>
    <Capability Name="internetClient"/>
    <rescap:Capability Name="runFullTrust"/>
    <rescap:Capability Name="localSystemServices"/>
  </Capabilities>
</Package>
```

**Note on MSIX building:** Building `.wapproj` may require the Windows 10 SDK (19041+) and the "Windows Application Packaging Project" workload. If the Feature Developer cannot build the `.wapproj` from command line (it requires specific VS tooling), this is acceptable — the project file and manifest can be validated manually. The critical acceptance criterion is that the `.wapproj` and `.appxmanifest` files exist with correct content.

---

### Step 9: Create GitHub Actions CI/CD Pipeline

**External references needed:**
- [GitHub Actions setup-dotnet](https://github.com/actions/setup-dotnet) — .NET SDK installation
- [GitHub Actions checkout](https://github.com/actions/checkout) — repository checkout
- [.NET CLI in CI](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build) — `dotnet build` in CI environments

**Key commands:**
```bash
mkdir .github\workflows

# Create ci.yml manually (no CLI command for this)
```

**`ci.yml` content:**
```yaml
name: CI Build & Test

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: windows-latest
    name: Build & Test (.NET 8.0, Windows)

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup .NET 8.0 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore NuGet packages
        run: dotnet restore MySecondBrain.sln

      - name: Build solution
        run: dotnet build MySecondBrain.sln --configuration Release --no-restore

      - name: Run unit tests
        run: dotnet test tests/unit/MySecondBrain.Tests.Unit/MySecondBrain.Tests.Unit.csproj --configuration Release --no-build --verbosity normal

      - name: Run integration tests
        run: dotnet test tests/integration/MySecondBrain.Tests.Integration/MySecondBrain.Tests.Integration.csproj --configuration Release --no-build --verbosity normal
```

**Note on CI execution:** MSIX project may not build in CI without Windows SDK. If the `.wapproj` fails in CI, it can be excluded from the solution build with a configuration-specific condition or built in a separate job. This can be addressed when the first CI run reveals any issues.

---

### Step 10: Full Solution Build Verification

**External references needed:**
- No new references. This step validates everything from Steps 1-9.

**Key commands:**
```bash
# Full restore
dotnet restore MySecondBrain.sln

# Full debug build
dotnet build MySecondBrain.sln

# Full release build
dotnet build MySecondBrain.sln --configuration Release

# Run all tests
dotnet test MySecondBrain.sln

# Verify gitignore works
git status  # should show no bin/ or obj/ directories
```

**`.gitignore` additions (verify these exist):**
```
# Build outputs (already present in .gitignore — verify)
*.user
*.suo

# Test results (may need to add)
TestResults/
*.trx
*.coverage

# MSIX outputs
*.msix
*.msixbundle
*.appinstaller
*.appxsym
```

**Acceptance checklist:**
- [ ] 7 projects in solution (`dotnet sln list`)
- [ ] 0 build errors (debug + release)
- [ ] 0 build warnings (debug + release) — `TreatWarningsAsErrors=true` enforces this
- [ ] 15+ NuGet packages resolved without version conflicts
- [ ] All 4 production project references wired: Core ← Data ← Services ← UI
- [ ] Test projects reference all production projects
- [ ] MSIX project references UI project
- [ ] build artifacts git-ignored
- [ ] `dotnet test` completes successfully (0 tests, no errors)
