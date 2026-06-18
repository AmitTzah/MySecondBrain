using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.Services;

public class HunspellSpellCheckService : ISpellCheckService
{
    public bool IsEnabled { get; set; }

    public bool CheckWord(string word) => false;

    public IReadOnlyList<string> GetSuggestions(string word) => Array.Empty<string>();

    public void AddToCustomDictionary(string word) { }

    public void RemoveFromCustomDictionary(string word) { }

    public bool IsInCustomDictionary(string word) => false;

    public void SetLanguage(string languageCode) { }
}
