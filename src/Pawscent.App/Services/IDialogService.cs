using Pawscent.App.ViewModels;
using Pawscent.Core.Profiles;

namespace Pawscent.App.Services;

/// <summary>Lets view models request UI without referencing WinUI types.</summary>
public interface IDialogService
{
    Task<string?> PromptNewProfileNameAsync();

    Task<Profile?> PickProfileAsync(IReadOnlyList<Profile> profiles);

    Task ShowMessageAsync(string title, string message);

    /// <summary>Asks a yes/no question. Returns true if the user chose <paramref name="confirmText"/>.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    /// <summary>
    /// Asks for one line of text. <paramref name="validate"/> returns null for valid text, otherwise a reason; the
    /// confirm button stays disabled until it passes. Returns null if cancelled.
    /// </summary>
    Task<string?> PromptTextAsync(
        string title, string label, string initialText, string confirmText, Func<string, string?> validate);

    /// <summary>Shows the add/edit expense dialog. Returns true if the expense was saved.</summary>
    Task<bool> EditExpenseAsync(ExpenseEditorViewModel editor);

    /// <summary>Lets the user choose a receipt file. Returns its full path, or null if cancelled.</summary>
    Task<string?> PickReceiptFileAsync();

    /// <summary>
    /// Asks where to save a PDF report, starting from <paramref name="suggestedName"/> (no extension). Returns the full
    /// path, or null if cancelled.
    /// </summary>
    Task<string?> PickReportFileAsync(string suggestedName);
}
