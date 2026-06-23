using Xunit.Abstractions;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// E2E tests for Feature 7: Model Configurations & API Keys.
/// Tests the full create → verify → delete (self-cleaning) lifecycle for API keys and
/// model configurations within the Settings → Providers and Settings → Profiles categories.
///
/// All tests are self-cleaning: they create data, verify it exists, then delete via 🗑️
/// buttons and confirm the deletion, all within the same [Fact].
/// </summary>
[Collection("E2E")]
public sealed class ModelConfigsApiKeysE2ETests : E2eTestBase
{
    public ModelConfigsApiKeysE2ETests(E2eFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    // ============================================================
    // API Keys — Create, Test, Self-Clean
    // ============================================================

    [Fact]
    public async Task AddApiKey_ShouldSaveAndTest()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Providers");

        // Click Add API Key
        var addBtn = FindById("AddApiKeyButton");
        Assert.NotNull(addBtn);
        addBtn!.Click();
        await Task.Delay(500);

        // Verify form appeared
        var formTitle = FindByName("Add API Key");
        Assert.NotNull(formTitle);
        _output.WriteLine("API Key form opened with title 'Add API Key'.");

        // Select OpenAI provider
        var providerCombo = FindById("ProviderTypeCombo")?.AsComboBox();
        Assert.NotNull(providerCombo);
        providerCombo!.Expand();
        var providerItems = providerCombo.Items;
        Assert.True(providerItems.Length > 0, "ProviderTypeCombo should have items.");
        providerItems[0].Click();
        providerCombo.Collapse();
        await Task.Delay(300);

        // Fill in display name
        var displayInput = FindById("DisplayNameInput")?.AsTextBox();
        Assert.NotNull(displayInput);
        displayInput!.Text = "E2E Test API Key";
        _output.WriteLine("Entered display name.");

        // Fill in API key (PasswordBox uses Value pattern)
        SetPasswordInput("ApiKeyInput", "sk-e2e-test-key-12345");
        await Task.Delay(300);

        // Click Test Key button — verifies button is clickable and re-enables
        var testBtn = FindById("TestKeyButton");
        Assert.NotNull(testBtn);
        Assert.True(testBtn!.IsEnabled, "TestKeyButton should be enabled before testing.");
        testBtn.Click();
        await Task.Delay(1500); // Allow time for test attempt (even if it fails, button re-enables)

        // Verify button re-enabled after test attempt
        var testBtnAfter = FindById("TestKeyButton");
        Assert.NotNull(testBtnAfter);
        Assert.True(testBtnAfter!.IsEnabled, "TestKeyButton should re-enable after test attempt.");
        _output.WriteLine("TestKeyButton clicked and re-enabled.");

        // Save
        var saveBtn = FindById("SaveApiKeyButton");
        Assert.NotNull(saveBtn);
        saveBtn!.Click();
        await Task.Delay(800);

        // Verify key appears in list
        var savedItem = FindByNameContains("E2E Test API Key");
        Assert.NotNull(savedItem);
        _output.WriteLine("API Key saved and visible in list.");

        // Self-clean: delete via 🗑️
        var deleteBtn = savedItem!.FindFirstDescendant(
            _cf.ByControlType(ControlType.Button).And(_cf.ByName("🗑️")));
        Assert.NotNull(deleteBtn);
        deleteBtn!.Click();
        await Task.Delay(500);
        ConfirmMessageBox("Yes");
        await Task.Delay(500);

        // Verify deleted
        var afterDelete = FindByNameContains("E2E Test API Key", timeout: DefaultTimeout);
        Assert.Null(afterDelete);
        _output.WriteLine("API Key self-cleaning delete verified.");
    }

    // ============================================================
    // API Keys — Edit Display Name
    // ============================================================

    [Fact]
    public async Task EditApiKey_ShouldUpdateDisplayName()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Providers");

        // Create an API key first
        var addBtn = FindById("AddApiKeyButton");
        Assert.NotNull(addBtn);
        addBtn!.Click();
        await Task.Delay(500);

        var providerCombo = FindById("ProviderTypeCombo")?.AsComboBox();
        Assert.NotNull(providerCombo);
        providerCombo!.Expand();
        if (providerCombo.Items.Length > 0)
            providerCombo.Items[0].Click();
        providerCombo.Collapse();
        await Task.Delay(200);

        var displayInput = FindById("DisplayNameInput")?.AsTextBox();
        Assert.NotNull(displayInput);
        displayInput!.Text = "E2E Key Before Edit";

        SetPasswordInput("ApiKeyInput", "sk-e2e-edit-test");
        await Task.Delay(200);

        var saveBtn = FindById("SaveApiKeyButton");
        Assert.NotNull(saveBtn);
        saveBtn!.Click();
        await Task.Delay(800);

        var savedItem = FindByNameContains("E2E Key Before Edit");
        Assert.NotNull(savedItem);
        _output.WriteLine("API Key created for edit test.");

        // Find and click edit (✏️) button on the saved item
        var editBtn = savedItem!.FindFirstDescendant(
            _cf.ByControlType(ControlType.Button).And(_cf.ByName("✏️")));
        Assert.NotNull(editBtn);
        editBtn!.Click();
        await Task.Delay(500);

        // Change display name in the form
        var editDisplayInput = FindById("DisplayNameInput")?.AsTextBox();
        Assert.NotNull(editDisplayInput);
        editDisplayInput!.Text = "E2E Key After Edit";
        await Task.Delay(200);

        // Save the edited key
        var saveEditedBtn = FindById("SaveApiKeyButton");
        Assert.NotNull(saveEditedBtn);
        saveEditedBtn!.Click();
        await Task.Delay(800);

        // Verify new name appears
        var editedItem = FindByNameContains("E2E Key After Edit");
        Assert.NotNull(editedItem);
        var oldItem = FindByNameContains("E2E Key Before Edit", timeout: TimeSpan.FromSeconds(1));
        Assert.Null(oldItem);
        _output.WriteLine("API Key display name updated successfully.");

        // Self-clean: delete via 🗑️
        var deleteBtn = editedItem!.FindFirstDescendant(
            _cf.ByControlType(ControlType.Button).And(_cf.ByName("🗑️")));
        Assert.NotNull(deleteBtn);
        deleteBtn!.Click();
        await Task.Delay(500);
        ConfirmMessageBox("Yes");
        await Task.Delay(500);

        var afterDelete = FindByNameContains("E2E Key After Edit", timeout: DefaultTimeout);
        Assert.Null(afterDelete);
        _output.WriteLine("Edited API Key self-cleaning delete verified.");
    }

    // ============================================================
    // Model Configurations — Create, Verify, Self-Clean
    // Creates a temporary API key to populate the provider dropdown,
    // then creates a model config using it, verifies, and cleans up both.
    // ============================================================

    [Fact]
    public async Task AddModelConfig_ShouldSaveAndSelfDelete()
    {
        await UseSharedAppAsync();

        // Step 1: Create a temporary API key for the provider dropdown
        NavigateToSettings();
        SelectSettingsCategory("Providers");

        var addBtn = FindById("AddApiKeyButton");
        Assert.NotNull(addBtn);
        addBtn!.Click();
        await Task.Delay(400);

        var providerCombo = FindById("ProviderTypeCombo")?.AsComboBox();
        Assert.NotNull(providerCombo);
        providerCombo!.Expand();
        if (providerCombo.Items.Length > 0)
            providerCombo.Items[0].Click();
        providerCombo.Collapse();
        await Task.Delay(200);

        var displayInput = FindById("DisplayNameInput")?.AsTextBox();
        Assert.NotNull(displayInput);
        displayInput!.Text = "E2E Temp API Key for Model";

        SetPasswordInput("ApiKeyInput", "sk-e2e-temp-model-key");
        await Task.Delay(200);

        var saveApiBtn = FindById("SaveApiKeyButton");
        Assert.NotNull(saveApiBtn);
        saveApiBtn!.Click();
        await Task.Delay(600);

        var savedApiKey = FindByNameContains("E2E Temp API Key for Model");
        Assert.NotNull(savedApiKey);
        _output.WriteLine("Temporary API Key created for model config test.");

        // Step 2: Navigate to Profiles
        SelectSettingsCategory("Profiles");
        await Task.Delay(400);

        // Step 3: Create model config
        var addModelBtn = FindById("AddModelConfigButton");
        Assert.NotNull(addModelBtn);
        addModelBtn!.Click();
        await Task.Delay(500);

        // Fill Display Name
        var modelNameInput = FindById("ModelConfigDisplayNameInput")?.AsTextBox();
        Assert.NotNull(modelNameInput);
        modelNameInput!.Text = "E2E Test Model Config";
        _output.WriteLine("Entered model config display name.");
        await Task.Delay(200);

        // Select the API key from the provider combo
        var modelProviderCombo = FindById("ModelConfigProviderCombo")?.AsComboBox();
        Assert.NotNull(modelProviderCombo);
        modelProviderCombo!.Expand();
        if (modelProviderCombo.Items.Length > 0)
        {
            modelProviderCombo.Items[0].Click();
            _output.WriteLine("Selected API key for model config.");
        }
        modelProviderCombo.Collapse();
        await Task.Delay(200);

        // Enter model identifier in the editable combo
        var modelIdCombo = FindById("ModelIdentifierCombo")?.AsComboBox();
        Assert.NotNull(modelIdCombo);
        Assert.NotNull(modelIdCombo.Patterns.Value);
        modelIdCombo.Patterns.Value.Pattern.SetValue("gpt-4o");
        await Task.Delay(200);
        _output.WriteLine("Entered model identifier.");

        // Select context overflow strategy: SlidingWindow
        var overflowCombo = FindById("ModelConfigContextOverflowCombo")?.AsComboBox();
        Assert.NotNull(overflowCombo);
        overflowCombo!.Expand();
        var overflowItems = overflowCombo.Items;
        // Find "SlidingWindow" in the items
        var slidingItem = overflowItems.FirstOrDefault(i =>
            i.Name?.Contains("SlidingWindow", StringComparison.OrdinalIgnoreCase) == true);
        if (slidingItem != null)
        {
            slidingItem.Click();
            _output.WriteLine("Selected SlidingWindow overflow strategy.");
        }
        else if (overflowItems.Length > 0)
        {
            overflowItems[0].Click();
        }
        overflowCombo.Collapse();
        await Task.Delay(200);

        // Save model config
        var saveModelBtn = FindById("SaveModelConfigButton");
        Assert.NotNull(saveModelBtn);
        saveModelBtn!.Click();
        await Task.Delay(800);

        // Verify model config appears in list
        var savedModel = FindByNameContains("E2E Test Model Config");
        Assert.NotNull(savedModel);
        _output.WriteLine("Model Config saved and visible in list.");

        // Step 4: Self-clean — delete model config via 🗑️
        var modelDeleteBtn = savedModel!.FindFirstDescendant(
            _cf.ByControlType(ControlType.Button).And(_cf.ByName("🗑️")));
        Assert.NotNull(modelDeleteBtn);
        modelDeleteBtn!.Click();
        await Task.Delay(500);
        ConfirmMessageBox("Yes");
        await Task.Delay(600);

        // Verify model config deleted
        var modelAfterDelete = FindByNameContains("E2E Test Model Config", timeout: DefaultTimeout);
        Assert.Null(modelAfterDelete);
        _output.WriteLine("Model Config self-cleaning delete verified.");

        // Step 5: Switch to Providers to clean up temp API key
        SelectSettingsCategory("Providers");
        await Task.Delay(300);

        var apiKeyInProviders = FindByNameContains("E2E Temp API Key for Model");
        if (apiKeyInProviders != null)
        {
            var apiDeleteBtn = apiKeyInProviders.FindFirstDescendant(
                _cf.ByControlType(ControlType.Button).And(_cf.ByName("🗑️")));
            if (apiDeleteBtn != null)
            {
                apiDeleteBtn.Click();
                await Task.Delay(500);
                ConfirmMessageBox("Yes");
                await Task.Delay(500);
                _output.WriteLine("Temporary API Key cleaned up.");
            }
        }
    }

    // ============================================================
    // Model Configurations — Duplicate Creates Copy
    // ============================================================

    [Fact]
    public async Task DuplicateModelConfig_ShouldCreateCopy()
    {
        await UseSharedAppAsync();

        // Create a temp API key and model config (same pattern as above)
        NavigateToSettings();
        SelectSettingsCategory("Providers");

        var addBtn = FindById("AddApiKeyButton");
        Assert.NotNull(addBtn);
        addBtn!.Click();
        await Task.Delay(400);

        var providerCombo = FindById("ProviderTypeCombo")?.AsComboBox();
        Assert.NotNull(providerCombo);
        providerCombo!.Expand();
        if (providerCombo.Items.Length > 0)
            providerCombo.Items[0].Click();
        providerCombo.Collapse();
        await Task.Delay(200);

        var displayInput = FindById("DisplayNameInput")?.AsTextBox();
        Assert.NotNull(displayInput);
        displayInput!.Text = "E2E Temp Key for Duplicate";
        SetPasswordInput("ApiKeyInput", "sk-e2e-dup-key");
        await Task.Delay(200);

        var saveApiBtn = FindById("SaveApiKeyButton");
        Assert.NotNull(saveApiBtn);
        saveApiBtn!.Click();
        await Task.Delay(600);

        SelectSettingsCategory("Profiles");
        await Task.Delay(300);

        // Create the original model config
        var addModelBtn = FindById("AddModelConfigButton");
        Assert.NotNull(addModelBtn);
        addModelBtn!.Click();
        await Task.Delay(500);

        var modelNameInput = FindById("ModelConfigDisplayNameInput")?.AsTextBox();
        Assert.NotNull(modelNameInput);
        modelNameInput!.Text = "E2E Duplicate Original";
        await Task.Delay(200);

        // Select the API key from the provider combo
        var modelProviderCombo = FindById("ModelConfigProviderCombo")?.AsComboBox();
        Assert.NotNull(modelProviderCombo);
        modelProviderCombo!.Expand();
        if (modelProviderCombo.Items.Length > 0)
        {
            modelProviderCombo.Items[0].Click();
            _output.WriteLine("Selected API key for duplicate model config.");
        }
        modelProviderCombo.Collapse();
        await Task.Delay(200);

        // Enter model identifier in the editable combo
        var modelIdCombo = FindById("ModelIdentifierCombo")?.AsComboBox();
        Assert.NotNull(modelIdCombo);
        Assert.NotNull(modelIdCombo.Patterns.Value);
        modelIdCombo.Patterns.Value.Pattern.SetValue("gpt-4o-mini");
        await Task.Delay(200);

        // Select context overflow strategy
        var overflowCombo = FindById("ModelConfigContextOverflowCombo")?.AsComboBox();
        Assert.NotNull(overflowCombo);
        overflowCombo!.Expand();
        if (overflowCombo.Items.Length > 0)
            overflowCombo.Items[0].Click();
        overflowCombo.Collapse();
        await Task.Delay(200);

        var saveModelBtn = FindById("SaveModelConfigButton");
        Assert.NotNull(saveModelBtn);
        saveModelBtn!.Click();
        await Task.Delay(800);

        var originalItem = FindByNameContains("E2E Duplicate Original");
        Assert.NotNull(originalItem);
        _output.WriteLine("Original model config saved.");

        // Click 📋 Duplicate button on the original item
        var duplicateBtn = originalItem!.FindFirstDescendant(
            _cf.ByControlType(ControlType.Button).And(_cf.ByName("📋")));
        Assert.NotNull(duplicateBtn);
        duplicateBtn!.Click();
        await Task.Delay(800);

        // Verify "(Copy)" appears
        var copyItem = FindByNameContains("(Copy)");
        Assert.NotNull(copyItem);
        _output.WriteLine("Duplicate model config with '(Copy)' suffix created.");

        // Self-clean: delete both model configs
        // Delete the copy first
        var copyDeleteBtn = copyItem!.FindFirstDescendant(
            _cf.ByControlType(ControlType.Button).And(_cf.ByName("🗑️")));
        if (copyDeleteBtn != null)
        {
            copyDeleteBtn.Click();
            await Task.Delay(500);
            ConfirmMessageBox("Yes");
            await Task.Delay(600);
        }

        // Delete the original
        var origAfterDup = FindByNameContains("E2E Duplicate Original", timeout: DefaultTimeout);
        if (origAfterDup != null)
        {
            var origDeleteBtn = origAfterDup.FindFirstDescendant(
                _cf.ByControlType(ControlType.Button).And(_cf.ByName("🗑️")));
            if (origDeleteBtn != null)
            {
                origDeleteBtn.Click();
                await Task.Delay(500);
                ConfirmMessageBox("Yes");
                await Task.Delay(600);
            }
        }

        // Cleanup temp API key
        SelectSettingsCategory("Providers");
        await Task.Delay(300);

        var apiKeyItem = FindByNameContains("E2E Temp Key for Duplicate");
        if (apiKeyItem != null)
        {
            var apiDeleteBtn = apiKeyItem.FindFirstDescendant(
                _cf.ByControlType(ControlType.Button).And(_cf.ByName("🗑️")));
            if (apiDeleteBtn != null)
            {
                apiDeleteBtn.Click();
                await Task.Delay(500);
                ConfirmMessageBox("Yes");
                await Task.Delay(500);
            }
        }

        _output.WriteLine("Duplicate test cleanup complete — all items deleted.");
    }

    // ============================================================
    // Model Configurations — Editable Combo Supports Text Entry
    // ============================================================

    [Fact]
    public async Task ModelConfig_ShouldSupportEditableCombo()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Profiles");

        var addModelBtn = FindById("AddModelConfigButton");
        Assert.NotNull(addModelBtn);
        addModelBtn!.Click();
        await Task.Delay(500);

        // ModelIdentifierCombo has IsEditable="True" — verify text can be typed
        var modelIdCombo = FindById("ModelIdentifierCombo")?.AsComboBox();
        Assert.NotNull(modelIdCombo);

        // Type text into the editable portion
        Assert.NotNull(modelIdCombo.Patterns.Value);
        modelIdCombo.Patterns.Value.Pattern.SetValue("custom-model-v2");
        await Task.Delay(200);

        // Verify the text was accepted
        Assert.Equal("custom-model-v2", modelIdCombo.Patterns.Value.Pattern.Value);
        _output.WriteLine($"ModelIdentifierCombo editable text set to '{modelIdCombo.Patterns.Value.Pattern.Value}'.");

        // Cancel to leave clean state (no save)
        var cancelBtn = FindByName("Cancel");
        if (cancelBtn != null)
        {
            cancelBtn.Click();
            await Task.Delay(300);
        }

        _output.WriteLine("Editable ComboBox supports direct text entry.");
    }

    // ============================================================
    // Model Configurations — Context Overflow Strategies
    // ============================================================

    [Fact]
    public async Task ContextOverflowStrategies_ShouldBeAvailable()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        SelectSettingsCategory("Profiles");

        var addModelBtn = FindById("AddModelConfigButton");
        Assert.NotNull(addModelBtn);
        addModelBtn!.Click();
        await Task.Delay(500);

        var overflowCombo = FindById("ModelConfigContextOverflowCombo")?.AsComboBox();
        Assert.NotNull(overflowCombo);

        // Expand to see options
        overflowCombo!.Expand();
        await Task.Delay(300);

        var items = overflowCombo.Items;
        var itemNames = items.Select(i => i.Name).ToArray();
        _output.WriteLine($"Context overflow strategies: [{string.Join(", ", itemNames)}]");

        // Verify expected strategies exist
        Assert.Contains(itemNames, n => n.Contains("SlidingWindow", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(itemNames, n => n.Contains("HardStop", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(itemNames, n => n.Contains("AutoSummarize", StringComparison.OrdinalIgnoreCase));
        _output.WriteLine("All 3 context overflow strategies are available.");

        // Select SlidingWindow to verify selection
        var slidingItem = items.FirstOrDefault(i =>
            i.Name.Contains("SlidingWindow", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(slidingItem);
        slidingItem!.Click();
        overflowCombo.Collapse();
        await Task.Delay(200);

        // Cancel to leave clean state
        var cancelBtn = FindByName("Cancel");
        if (cancelBtn != null)
        {
            cancelBtn.Click();
            await Task.Delay(300);
        }

        _output.WriteLine("ContextOverflowStrategies: SlidingWindow selectable.");
    }
}
