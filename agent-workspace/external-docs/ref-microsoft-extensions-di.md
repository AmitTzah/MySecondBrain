# Microsoft.Extensions.DependencyInjection — Key Patterns

> Source: Context7 MCP query against `/dotnet/extensions`. Saved 2026-06-17.

## Core Registration Methods

### IServiceCollection Extension Methods (Microsoft.Extensions.DependencyInjection)

```csharp
// Singleton — one instance per container lifetime
services.AddSingleton<TService, TImplementation>();
services.AddSingleton<TService>(implementationInstance);
services.AddSingleton<TService>(sp => new TImplementation(sp.GetRequiredService<TOther>()));

// Transient — new instance every time
services.AddTransient<TService, TImplementation>();
services.AddTransient<TService>(sp => new TImplementation());

// Scoped — new instance per scope (not used in MySecondBrain)
services.AddScoped<TService, TImplementation>();
```

### Multiple Implementations of Same Interface

Registering multiple implementations of the same interface (used for IContentBlockRenderer, ILLMProvider, etc.):

```csharp
// Each registration adds to the collection
services.AddSingleton<IContentBlockRenderer, MarkdownTextRenderer>();
services.AddSingleton<IContentBlockRenderer, CodeBlockRenderer>();
services.AddSingleton<IContentBlockRenderer, ArtifactReferenceRenderer>();
// ...

// Resolve all via IEnumerable<T>
public class ContentRendererRegistry : IContentRendererRegistry
{
    public ContentRendererRegistry(IEnumerable<IContentBlockRenderer> renderers)
    {
        // All 7 renderers injected here automatically
    }
}
```

### ServiceCollection.BuildServiceProvider

```csharp
var services = new ServiceCollection();
// ... register
IServiceProvider serviceProvider = services.BuildServiceProvider();

// Validate via BuildServiceProvider with ValidateScopes and ValidateOnBuild
var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateScopes = true,
    ValidateOnBuild = true  // Catches missing registrations at startup
});
```

### GetRequiredService / GetService

```csharp
// Throws InvalidOperationException if not registered
var svc = serviceProvider.GetRequiredService<IChatThreadService>();

// Returns null if not registered
var svc = serviceProvider.GetService<IChatThreadService>();
```

### EF Core DbContext Registration

```csharp
var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "MySecondBrain",
    "msb.db");

services.AddSingleton<AppDbContext>(sp =>
{
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={dbPath}")
        .Options;
    return new AppDbContext(options);
});
```

### Host Builder Pattern (Optional)

For IHostedService support, use Microsoft.Extensions.Hosting:

```csharp
var host = Host.CreateDefaultBuilder()
    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<IChatThreadService, ChatThreadService>();
        // ...
    })
    .Build();

// Or in WPF, use ServiceCollection directly and manually start IHostedService instances
```

## Key Namespaces

- `Microsoft.Extensions.DependencyInjection` — IServiceCollection, ServiceCollection, ServiceProvider
- `Microsoft.Extensions.Hosting` — IHost, IHostedService, Host
- `Microsoft.Extensions.Logging` — ILogger<T>, ILoggerFactory
