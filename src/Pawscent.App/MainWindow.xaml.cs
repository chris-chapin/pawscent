using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pawscent.App.Services;
using Pawscent.App.ViewModels;
using Pawscent.App.Views;
using Pawscent.Core.Profiles;

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
        };
        AppWindow.Closing += AppWindow_Closing;
    }

    public MainViewModel ViewModel { get; }

    public Task<string?> PromptNewProfileNameAsync() =>
        new NewProfileDialog(_profiles) { XamlRoot = RootGrid.XamlRoot }.ShowForNameAsync();

    public Task<Profile?> PickProfileAsync(IReadOnlyList<Profile> profiles) =>
        new LoadProfileDialog(profiles) { XamlRoot = RootGrid.XamlRoot }.ShowForProfileAsync();

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
