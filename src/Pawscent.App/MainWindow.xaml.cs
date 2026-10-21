using Microsoft.UI.Xaml;
using Pawscent.App.Services;
using Pawscent.App.ViewModels;
using Pawscent.App.Views;
using Pawscent.Core.Profiles;

namespace Pawscent.App;

public sealed partial class MainWindow : Window, IDialogService
{
    private readonly IProfileService _profiles;

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
    }

    public MainViewModel ViewModel { get; }

    public Task<string?> PromptNewProfileNameAsync() =>
        new NewProfileDialog(_profiles) { XamlRoot = RootGrid.XamlRoot }.ShowForNameAsync();

    public Task<Profile?> PickProfileAsync(IReadOnlyList<Profile> profiles) =>
        new LoadProfileDialog(profiles) { XamlRoot = RootGrid.XamlRoot }.ShowForProfileAsync();

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = RootGrid.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
