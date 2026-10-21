using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pawscent.App.ViewModels;

namespace Pawscent.App.Views;

public sealed partial class YearView : UserControl
{
    public YearView(YearViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public YearViewModel ViewModel { get; }

    private void Expenses_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.EditExpenseCommand.CanExecute(null))
            ViewModel.EditExpenseCommand.Execute(null);
    }
}
