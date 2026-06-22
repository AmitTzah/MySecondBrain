using Serilog.Core;
using Serilog.Events;

namespace MySecondBrain.Services.Logging;

/// <summary>
/// Serilog ILogEventEnricher that redacts API key values from all log event properties.
/// Unlike IDestructuringPolicy (which Serilog does not invoke for string values even
/// with the @ operator), this enricher operates on the fully-constructed LogEvent and
/// replaces any ScalarValue string property that matches a known API key pattern with
/// "[REDACTED]".
/// </summary>
public class ApiKeyRedactionEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var propertiesToRedact = new List<string>();

        foreach (var kvp in logEvent.Properties)
        {
            if (kvp.Value is ScalarValue sv
                && sv.Value is string str
                && ApiKeyDestructuringPolicy.IsApiKeyString(str))
            {
                propertiesToRedact.Add(kvp.Key);
            }
        }

        foreach (var key in propertiesToRedact)
        {
            var redacted = propertyFactory.CreateProperty(key, "[REDACTED]");
            logEvent.AddOrUpdateProperty(redacted);
        }
    }
}
