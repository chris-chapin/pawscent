using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pawscent.App.ViewModels;

namespace Pawscent.App.Views;

public sealed partial class CategoriesView : UserControl
{
    public CategoriesView(CategoriesViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public CategoriesViewModel ViewModel { get; }

    private void Items_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.RenameCommand.CanExecute(null))
            ViewModel.RenameCommand.Execute(null);
    }
}
