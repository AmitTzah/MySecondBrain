using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MySecondBrain.UI.ViewModels;

public enum FileViewerType
{
    Text,
    Code,
    Markdown,
    Image
}

public partial class FileViewerTabViewModel : ObservableObject
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileContent = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private FileViewerType _fileType;

    [ObservableProperty]
    private bool _isReadOnly = true;

    /// <summary>
    /// The code language identifier for syntax highlighting (e.g., "csharp", "python").
    /// Only set when <see cref="FileType"/> is Code.
    /// </summary>
    public string? CodeLanguage { get; private set; }

    /// <summary>Display title for the tab header.</summary>
    public string Title => $"📄 {FileName}";

    private static readonly HashSet<string> CodeExtensions =
    [
        ".cs", ".py", ".js", ".ts", ".json", ".xml", ".yaml", ".yml",
        ".html", ".css", ".java", ".cpp", ".h", ".rs", ".go",
        ".rb", ".php", ".sql", ".sh", ".ps1", ".bat", ".ini",
        ".cfg", ".toml", ".sln", ".csproj", ".xaml", ".razor", ".kt", ".swift"
    ];

    private static readonly Dictionary<string, string> ExtensionToLanguage = new()
    {
        [".cs"] = "csharp",
        [".py"] = "python",
        [".js"] = "javascript",
        [".ts"] = "typescript",
        [".json"] = "json",
        [".xml"] = "xml",
        [".yaml"] = "yaml",
        [".yml"] = "yaml",
        [".html"] = "html",
        [".css"] = "css",
        [".java"] = "java",
        [".cpp"] = "cpp",
        [".h"] = "c",
        [".rs"] = "rust",
        [".go"] = "go",
        [".rb"] = "ruby",
        [".php"] = "php",
        [".sql"] = "sql",
        [".sh"] = "bash",
        [".ps1"] = "powershell",
        [".bat"] = "batch",
        [".ini"] = "ini",
        [".cfg"] = "ini",
        [".toml"] = "toml",
        [".sln"] = "xml",
        [".csproj"] = "xml",
        [".xaml"] = "xml",
        [".razor"] = "razor",
        [".kt"] = "kotlin",
        [".swift"] = "swift",
    };

    public static async Task<FileViewerTabViewModel> FromFileAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var vm = new FileViewerTabViewModel
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            FileType = DetermineType(ext),
            CodeLanguage = ExtensionToLanguage.GetValueOrDefault(ext)
        };

        if (vm.FileType == FileViewerType.Image)
        {
            vm.FileContent = string.Empty;
        }
        else
        {
            vm.FileContent = await File.ReadAllTextAsync(filePath);
        }

        return vm;
    }

    private static FileViewerType DetermineType(string ext)
    {
        return ext switch
        {
            ".md" or ".markdown" => FileViewerType.Markdown,
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".ico" => FileViewerType.Image,
            _ when CodeExtensions.Contains(ext) => FileViewerType.Code,
            _ => FileViewerType.Text
        };
    }
}
