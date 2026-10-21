using CommunityToolkit.Mvvm.ComponentModel;

namespace Pawscent.App.ViewModels;

/// <summary>A category checkbox in a year's filter. Checked categories are shown.</summary>
public sealed class CategoryFilterItem(Guid categoryId, string name, bool isSelected) : ObservableObject
{
    private bool _isSelected = isSelected;

    public Guid CategoryId => categoryId;

    public string Name => name;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
