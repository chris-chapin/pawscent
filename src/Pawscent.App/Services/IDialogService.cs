using Pawscent.Core.Profiles;

namespace Pawscent.App.Services;

/// <summary>Lets view models request UI without referencing WinUI types.</summary>
public interface IDialogService
{
    Task<string?> PromptNewProfileNameAsync();

    Task<Profile?> PickProfileAsync(IReadOnlyList<Profile> profiles);

    Task ShowMessageAsync(string title, string message);
}
