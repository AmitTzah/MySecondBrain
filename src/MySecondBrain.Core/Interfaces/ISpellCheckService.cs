namespace MySecondBrain.Core.Interfaces;

public interface ISpellCheckService
{
    bool IsEnabled { get; set; }
    bool CheckWord(string word);
    IReadOnlyList<string> GetSuggestions(string word);
    void AddToCustomDictionary(string word);
    void RemoveFromCustomDictionary(string word);
    bool IsInCustomDictionary(string word);
    void SetLanguage(string languageCode);
}
