using System.Collections.Specialized;
using Microsoft.UI.Xaml.Controls;
using Pawscent.App.ViewModels;

namespace Pawscent.App.Views;

public sealed partial class ProfileView : UserControl
{
    public ProfileView(ProfileViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        CategoriesTab.Content = new CategoriesView(viewModel.Categories);
        ReceiptsTab.Content = new ReceiptsView(viewModel.Receipts);
        for (var i = 0; i < viewModel.Years.Count; i++)
            Tabs.TabItems.Insert(i, CreateYearTab(viewModel.Years[i]));
        viewModel.Years.CollectionChanged += Years_CollectionChanged;
        viewModel.YearRequested += (_, year) => Tabs.SelectedItem = FindYearTab(year);
        Tabs.SelectedItem = FindYearTab(viewModel.CurrentYear);
    }

    public ProfileViewModel ViewModel { get; }

    private static TabViewItem CreateYearTab(YearViewModel year) =>
        new()
        {
            Header = year.Header,
            IsClosable = false,
            IconSource = new SymbolIconSource { Symbol = Symbol.Calendar },
            Content = new YearView(year),
            Tag = year,
        };

    private TabViewItem? FindYearTab(YearViewModel year) =>
        Tabs.TabItems.OfType<TabViewItem>().FirstOrDefault(t => ReferenceEquals(t.Tag, year));

    // Year tabs are only ever inserted, at the same index as in the view model's list.
    private void Years_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null)
            return;
        var index = e.NewStartingIndex;
        foreach (var year in e.NewItems.OfType<YearViewModel>())
            Tabs.TabItems.Insert(index++, CreateYearTab(year));
    }
}
