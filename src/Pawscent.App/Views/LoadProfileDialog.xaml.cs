using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pawscent.Core.Profiles;

namespace Pawscent.App.Views;

public sealed partial class LoadProfileDialog : ContentDialog
{
    public LoadProfileDialog(IReadOnlyList<Profile> profiles)
    {
        InitializeComponent();
        ProfileList.ItemsSource = profiles;
    }

    private bool _acceptedByDoubleTap;

    public async Task<Profile?> ShowForProfileAsync()
    {
        var result = await ShowAsync();
        return result == ContentDialogResult.Primary || _acceptedByDoubleTap ? ProfileList.SelectedItem as Profile : null;
    }

    private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        IsPrimaryButtonEnabled = ProfileList.SelectedItem is not null;

    private void ProfileList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is not null)
        {
            _acceptedByDoubleTap = true;
            Hide();
        }
    }
}
