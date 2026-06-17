using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.LLM;

public class TokenizerFactory : ITokenizerFactory
{
    private readonly IEnumerable<ITokenizer> _tokenizers;
    private readonly ILogger<TokenizerFactory> _logger;

    public TokenizerFactory(
        IEnumerable<ITokenizer> tokenizers,
        ILogger<TokenizerFactory> logger)
    {
        _tokenizers = tokenizers;
        _logger = logger;
    }

    public ITokenizer GetTokenizer(string modelId, ProviderType provider) =>
        _tokenizers.FirstOrDefault(t => t.SupportsModel(modelId))!;

    public ITokenizer GetFallbackTokenizer() =>
        _tokenizers.FirstOrDefault(t => t is FallbackTokenizer)!;
}
