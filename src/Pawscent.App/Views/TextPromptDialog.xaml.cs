using Microsoft.UI.Xaml.Controls;

namespace Pawscent.App.Views;

public sealed partial class TextPromptDialog : ContentDialog
{
    private readonly Func<string, string?> _validate;

    public TextPromptDialog(string title, string label, string initialText, string confirmText, Func<string, string?> validate)
    {
        _validate = validate;
        InitializeComponent();
        Title = title;
        PrimaryButtonText = confirmText;
        InputBox.Header = label;
        InputBox.Text = initialText;
        InputBox.SelectAll();
    }

    public async Task<string?> ShowForTextAsync() =>
        await ShowAsync() == ContentDialogResult.Primary ? InputBox.Text : null;

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var error = InputBox.Text.Length == 0 ? null : _validate(InputBox.Text);
        ErrorText.Text = error ?? string.Empty;
        IsPrimaryButtonEnabled = InputBox.Text.Length > 0 && error is null;
    }
}
