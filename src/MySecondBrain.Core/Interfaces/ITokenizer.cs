namespace MySecondBrain.Core.Interfaces;

public interface ITokenizer
{
    string TokenizerName { get; }

    int CountTokens(string text);

    IReadOnlyList<int> Encode(string text);

    string Decode(IReadOnlyList<int> tokens);

    int MaxContextTokens { get; }

    bool SupportsModel(string modelId);
}
