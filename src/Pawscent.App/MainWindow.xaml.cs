using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pawscent.App.Services;
using Pawscent.App.ViewModels;
using Pawscent.App.Views;
using Pawscent.Core.Profiles;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Pawscent.App;

public sealed partial class MainWindow : Window, IDialogService
{
    private readonly IProfileService _profiles;
    private bool _closeConfirmed;

    public MainWindow(MainViewModel viewModel, IProfileService profiles)
    {
        _profiles = profiles;
        ViewModel = viewModel;
        ViewModel.Dialogs = this;
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 720));
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.WindowTitle))
                Title = ViewModel.WindowTitle;
            else if (e.PropertyName == nameof(MainViewModel.CurrentProfileView))
                ProfileHost.Content = ViewModel.CurrentProfileView is { } profile ? new ProfileView(profile) : null;
        };
        AppWindow.Closing += AppWindow_Closing;
    }

    public MainViewModel ViewModel { get; }

    public Task<string?> PromptNewProfileNameAsync() =>
        new NewProfileDialog(_profiles) { XamlRoot = RootGrid.XamlRoot }.ShowForNameAsync();

    public Task<Profile?> PickProfileAsync(IReadOnlyList<Profile> profiles) =>
        new LoadProfileDialog(profiles) { XamlRoot = RootGrid.XamlRoot }.ShowForProfileAsync();

    public Task<string?> PromptTextAsync(
        string title, string label, string initialText, string confirmText, Func<string, string?> validate) =>
        new TextPromptDialog(title, label, initialText, confirmText, validate) { XamlRoot = RootGrid.XamlRoot }
            .ShowForTextAsync();

    public Task<bool> EditExpenseAsync(ExpenseEditorViewModel editor) =>
        new ExpenseDialog(editor) { XamlRoot = RootGrid.XamlRoot }.ShowForSaveAsync();

    public async Task<string?> PickReceiptFileAsync()
    {
        var picker = new FileOpenPicker { ViewMode = PickerViewMode.List, SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add("*");
        // Unpackaged apps must tell the picker which window owns it.
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        return file is null || string.IsNullOrEmpty(file.Path) ? null : file.Path;
    }

    public async Task<string?> PickReportFileAsync(string suggestedName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedName,
        };
        picker.FileTypeChoices.Add("PDF document", new List<string> { ".pdf" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSaveFileAsync();
        return file is null || string.IsNullOrEmpty(file.Path) ? null : file.Path;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = RootGrid.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = confirmText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = RootGrid.XamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    // The title bar close button: hold the close until pending changes are saved.
    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed)
            return;
        args.Cancel = true;
        await CloseAfterSavingAsync();
    }

    private async void Exit_Click(object sender, RoutedEventArgs e) => await CloseAfterSavingAsync();

    private async Task CloseAfterSavingAsync()
    {
        if (!await ViewModel.PrepareToCloseAsync())
            return;
        _closeConfirmed = true;
        Close();
    }
}
