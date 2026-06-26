using System.IO;
using MySecondBrain.Core.Models;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.Tests.Unit;

public class DataLocationInfoTests
{
    [Theory]
    [InlineData(LocationEditability.AppManaged, "❌")]
    [InlineData(LocationEditability.Caution, "⚠️")]
    [InlineData(LocationEditability.UserEditable, "✅")]
    public void EditabilityIcon_ReturnsCorrectIcon(LocationEditability editability, string expected)
    {
        var info = new DataLocationInfo("test", null, "purpose", editability);
        Assert.Equal(expected, info.EditabilityIcon);
    }

    [Theory]
    [InlineData(LocationEditability.AppManaged, "No — app-managed")]
    [InlineData(LocationEditability.Caution, "Caution — editable but risky")]
    [InlineData(LocationEditability.UserEditable, "Yes — user-editable")]
    public void EditabilityLabel_ReturnsCorrectLabel(LocationEditability editability, string expected)
    {
        var info = new DataLocationInfo("test", null, "purpose", editability);
        Assert.Equal(expected, info.EditabilityLabel);
    }

    [Theory]
    [InlineData(LocationEditability.AppManaged,
        "Do not modify manually. This is managed internally by the application.")]
    [InlineData(LocationEditability.Caution,
        "Editable but changes may affect application behavior. Understand the consequences before modifying.")]
    [InlineData(LocationEditability.UserEditable,
        "Your own files — safe to modify, add, or remove.")]
    public void EditabilityTooltip_ReturnsCorrectTooltip(LocationEditability editability, string expected)
    {
        var info = new DataLocationInfo("test", null, "purpose", editability);
        Assert.Equal(expected, info.EditabilityTooltip);
    }

    [Fact]
    public void SizeOnDisk_DefaultsToCalculating()
    {
        var info = new DataLocationInfo("test", null, "purpose", LocationEditability.AppManaged);
        Assert.Equal("Calculating…", info.SizeOnDisk);
        Assert.False(info.IsSizeCalculated);
    }

    [Fact]
    public async Task CalculateSizeAsync_NullPath_SetsDash()
    {
        var info = new DataLocationInfo("test", null, "purpose", LocationEditability.AppManaged);
        await info.CalculateSizeAsync();
        Assert.Equal("—", info.SizeOnDisk);
        Assert.True(info.IsSizeCalculated);
    }

    [Fact]
    public async Task CalculateSizeAsync_NonExistentPath_SetsDash()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var info = new DataLocationInfo("test", path, "purpose", LocationEditability.AppManaged);
        await info.CalculateSizeAsync();
        Assert.Equal("—", info.SizeOnDisk);
        Assert.True(info.IsSizeCalculated);
    }

    [Fact]
    public async Task CalculateSizeAsync_ExistingFile_ReportsSize()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, new string('X', 2048)); // 2 KB
            var info = new DataLocationInfo("test", tempFile, "purpose", LocationEditability.AppManaged)
            {
                IsDirectory = false
            };
            await info.CalculateSizeAsync();
            Assert.Contains("KB", info.SizeOnDisk);
            Assert.True(info.IsSizeCalculated);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CalculateSizeAsync_ExistingDirectory_ReportsSize()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var file1 = Path.Combine(tempDir, "test1.txt");
            await File.WriteAllTextAsync(file1, new string('A', 1024));

            var subDir = Path.Combine(tempDir, "sub");
            Directory.CreateDirectory(subDir);
            var file2 = Path.Combine(subDir, "test2.txt");
            await File.WriteAllTextAsync(file2, new string('B', 512));

            var info = new DataLocationInfo("test", tempDir, "purpose", LocationEditability.AppManaged)
            {
                IsDirectory = true
            };
            await info.CalculateSizeAsync();
            Assert.Contains("KB", info.SizeOnDisk);
            Assert.True(info.IsSizeCalculated);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SetPendingNavigationCategory_DoesNotThrow()
    {
        SettingsViewModel.SetPendingNavigationCategory(SettingsCategory.SystemInfo);

        var exception = Record.Exception(() =>
            SettingsViewModel.SetPendingNavigationCategory(SettingsCategory.Diagnostics));
        Assert.Null(exception);
    }

    [Fact]
    public void DataLocationInfo_AutomationKey_IsSet()
    {
        var info = new DataLocationInfo("test", null, "purpose", LocationEditability.AppManaged)
        {
            AutomationKey = "TestKey"
        };
        Assert.Equal("TestKey", info.AutomationKey);
    }

    [Fact]
    public void DataLocationInfo_DisplayPath_IsSet()
    {
        var info = new DataLocationInfo("%TEST%/path", null, "purpose", LocationEditability.AppManaged);
        Assert.Equal("%TEST%/path", info.DisplayPath);
    }

    [Fact]
    public void DataLocationInfo_Purpose_IsSet()
    {
        var info = new DataLocationInfo("test", null, "My purpose", LocationEditability.AppManaged);
        Assert.Equal("My purpose", info.Purpose);
    }
}
