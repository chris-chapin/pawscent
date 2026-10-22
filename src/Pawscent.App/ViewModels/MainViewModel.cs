using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pawscent.App.Services;
using Pawscent.Core.Profiles;
using Pawscent.Core.Receipts;
using Pawscent.Core.Reports;

namespace Pawscent.App.ViewModels;

public sealed partial class MainViewModel(
    IProfileService profiles, ReceiptFiles receiptFiles, ExpenseReportGenerator reports, TimeProvider timeProvider)
    : ObservableObject
{
    private ProfileAutoSaver? _autoSaver;

    public IDialogService Dialogs { get; set; } = null!;

    /// <summary>The open profile's categories, receipts and expenses. Edits are saved automatically.</summary>
    public ProfileDocument? CurrentDocument
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
                CurrentProfile = value?.Profile;
        }
    }

    /// <summary>The tabs of the open profile, or null when no profile is open.</summary>
    public ProfileViewModel? CurrentProfileView
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(nameof(HasNoProfile));
        }
    }

    public bool HasNoProfile => CurrentProfileView is null;

    public Profile? CurrentProfile
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HeaderText));
                OnPropertyChanged(nameof(CurrentProfileFolder));
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public string HeaderText => CurrentProfile?.Name ?? "No profile loaded. Use File > New or File > Load.";

    public string CurrentProfileFolder => CurrentProfile?.FolderPath ?? string.Empty;

    public string WindowTitle => CurrentProfile is null ? "Pawscent" : $"Pawscent - {CurrentProfile.Name}";

    /// <summary>Saves pending changes before the app closes. Returns false if the user chose not to close.</summary>
    public async Task<bool> PrepareToCloseAsync()
    {
        try
        {
            _autoSaver?.SaveNow();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return await Dialogs.ConfirmAsync(
                "Could not save profile",
                $"{ex.Message}\n\nClose anyway and lose your latest changes?",
                "Close anyway");
        }
    }

    [RelayCommand]
    private async Task NewProfileAsync()
    {
        var name = await Dialogs.PromptNewProfileNameAsync();
        if (name is null || !await TrySaveCurrentAsync())
            return;

        Profile created;
        try
        {
            created = profiles.Create(name);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("Could not create profile", ex.Message);
            return;
        }
        await OpenProfileAsync(created.Name);
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
        if (picked is not null && await TrySaveCurrentAsync())
            await OpenProfileAsync(picked.Name);
    }

    private async Task OpenProfileAsync(string name)
    {
        ProfileDocument document;
        try
        {
            document = profiles.Open(name);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("Could not open profile", ex.Message);
            return;
        }

        if (_autoSaver is not null)
        {
            _autoSaver.SaveFailed -= OnAutoSaveFailed;
            _autoSaver.Dispose();
        }
        // Commands run on the UI thread, so timed saves are posted back to it and never overlap an edit.
        _autoSaver = new ProfileAutoSaver(
            document, profiles, ProfileAutoSaver.DefaultDelay, timeProvider, SynchronizationContext.Current);
        _autoSaver.SaveFailed += OnAutoSaveFailed;
        CurrentProfileView?.Dispose();
        CurrentDocument = document;
        CurrentProfileView = new ProfileViewModel(document, Dialogs, receiptFiles, reports, timeProvider);
    }

    // Flushes the open profile before switching away from it. Returns false if it could not be saved.
    private async Task<bool> TrySaveCurrentAsync()
    {
        try
        {
            _autoSaver?.SaveNow();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("Could not save profile", ex.Message);
            return false;
        }
    }

    private async void OnAutoSaveFailed(object? sender, Exception ex)
    {
        try
        {
            await Dialogs.ShowMessageAsync(
                "Could not save profile",
                $"{ex.Message}\n\nPawscent will try again after your next change.");
        }
        catch (COMException)
        {
            // Another dialog is already open; the save is retried on the next change or on close.
        }
    }
}
