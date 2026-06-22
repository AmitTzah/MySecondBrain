using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface ITextActionRepository
{
    Task<IReadOnlyList<TextAction>> GetAllAsync();
    Task<TextAction?> GetByIdAsync(string id);
    Task<TextAction> CreateAsync(TextAction action);
    Task UpdateAsync(TextAction action);
    Task DeleteAsync(string id);
    Task<IReadOnlyList<TextAction>> GetByHotkeyAsync(string hotkey);
}
