namespace MySecondBrain.Tests.E2E;

/// <summary>
/// xUnit collection definition for the "E2E" collection.
/// Enables ICollectionFixture<E2eFixture> so the app launches once for all tests.
/// Must be in the same assembly as the test classes using [Collection("E2E")].
/// </summary>
[CollectionDefinition("E2E")]
public sealed class E2eTestCollection : ICollectionFixture<E2eFixture>
{
    // This class has no code — it exists only to define the collection.
}
