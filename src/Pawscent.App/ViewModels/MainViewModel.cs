using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pawscent.App.Services;
using Pawscent.Core.Profiles;

namespace Pawscent.App.ViewModels;

public sealed partial class MainViewModel(IProfileService profiles) : ObservableObject
{
    public IDialogService Dialogs { get; set; } = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderText), nameof(CurrentProfileFolder), nameof(WindowTitle))]
    public partial Profile? CurrentProfile { get; set; }

    public string HeaderText => CurrentProfile?.Name ?? "No profile loaded. Use File > New or File > Load.";

    public string CurrentProfileFolder => CurrentProfile?.FolderPath ?? string.Empty;

    public string WindowTitle => CurrentProfile is null ? "Pawscent" : $"Pawscent - {CurrentProfile.Name}";

    [RelayCommand]
    private async Task NewProfileAsync()
    {
        var name = await Dialogs.PromptNewProfileNameAsync();
        if (name is null)
            return;

        try
        {
            CurrentProfile = profiles.Create(name);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("Could not create profile", ex.Message);
        }
    }

    [RelayCommand]
    private async Task LoadProfileAsync()
    {
        var existing = profiles.List();
        if (existing.Count == 0)
        {
            await Dialogs.ShowMessageAsync("No profiles", "There are no profiles yet. Use File > New > Profile to create one.");
            return;
        }

        var picked = await Dialogs.PickProfileAsync(existing);
        if (picked is not null)
            CurrentProfile = picked;
    }
}
