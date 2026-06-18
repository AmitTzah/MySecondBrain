using Xunit.Sdk;

namespace MySecondBrain.Tests.Unit;

/// <summary>
/// xUnit fact attribute that runs the test on an STA thread.
/// Required for WPF UI components like Window and controls.
/// </summary>
/// <remarks>
/// The discoverer type name string must stay in sync with <see cref="StaFactDiscoverer"/>.
/// If the class is renamed or moved, update the string below.
/// </remarks>
[XunitTestCaseDiscoverer("MySecondBrain.Tests.Unit.StaFactDiscoverer", "MySecondBrain.Tests.Unit")]
public sealed class StaFactAttribute : FactAttribute
{
}
