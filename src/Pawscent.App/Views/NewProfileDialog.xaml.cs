using Microsoft.UI.Xaml.Controls;
using Pawscent.Core.Profiles;

namespace Pawscent.App.Views;

public sealed partial class NewProfileDialog : ContentDialog
{
    private readonly IProfileService _profiles;

    public NewProfileDialog(IProfileService profiles)
    {
        _profiles = profiles;
        InitializeComponent();
    }

    public async Task<string?> ShowForNameAsync() =>
        await ShowAsync() == ContentDialogResult.Primary ? NameBox.Text : null;

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var error = NameBox.Text.Length == 0 ? null : _profiles.Validate(NameBox.Text);
        ErrorText.Text = error ?? string.Empty;
        IsPrimaryButtonEnabled = NameBox.Text.Length > 0 && error is null;
    }
}
