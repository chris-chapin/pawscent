using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pawscent.App.ViewModels;
using Windows.Globalization.NumberFormatting;
using Windows.System;

namespace Pawscent.App.Views;

public sealed partial class ExpenseDialog : ContentDialog
{
    public ExpenseDialog(ExpenseEditorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        AmountBox.NumberFormatter = new DecimalFormatter
        {
            FractionDigits = 2,
            IntegerDigits = 1,
            NumberRounder = new IncrementNumberRounder { Increment = 0.01, RoundingAlgorithm = RoundingAlgorithm.RoundHalfAwayFromZero },
        };
    }

    public ExpenseEditorViewModel ViewModel { get; }

    public async Task<bool> ShowForSaveAsync() => await ShowAsync() == ContentDialogResult.Primary;

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) =>
        args.Cancel = !ViewModel.TrySave();

    private void NewCategoryFlyout_Opened(object sender, object e)
    {
        NewCategoryBox.Text = string.Empty;
        NewCategoryBox.Focus(FocusState.Programmatic);
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e) => AddCategory();

    private void NewCategoryBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
            return;
        e.Handled = true;
        AddCategory();
    }

    private void AddCategory()
    {
        if (ViewModel.TryAddCategory(NewCategoryBox.Text))
            NewCategoryFlyout.Hide();
    }
}
