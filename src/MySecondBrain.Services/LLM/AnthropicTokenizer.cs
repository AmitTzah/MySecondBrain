using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.Services.LLM;

public class AnthropicTokenizer : ITokenizer
{
    private readonly ILogger<AnthropicTokenizer> _logger;

    public AnthropicTokenizer(ILogger<AnthropicTokenizer> logger)
    {
        _logger = logger;
    }

    public string TokenizerName => "Anthropic";

    public int CountTokens(string text) => 0;

    public IReadOnlyList<int> Encode(string text) => Array.Empty<int>();

    public string Decode(IReadOnlyList<int> tokens) => string.Empty;

    public int MaxContextTokens => 0;

    public bool SupportsModel(string modelId) => false;
}
