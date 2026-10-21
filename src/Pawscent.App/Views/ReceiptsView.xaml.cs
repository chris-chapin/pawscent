using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pawscent.App.ViewModels;

namespace Pawscent.App.Views;

public sealed partial class ReceiptsView : UserControl
{
    public ReceiptsView(ReceiptsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ReceiptsViewModel ViewModel { get; }

    private void Items_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.OpenCommand.CanExecute(null))
            ViewModel.OpenCommand.Execute(null);
    }
}
