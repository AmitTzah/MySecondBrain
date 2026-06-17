using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface ITokenizerFactory
{
    ITokenizer GetTokenizer(string modelId, ProviderType provider);
    ITokenizer GetFallbackTokenizer();
}
