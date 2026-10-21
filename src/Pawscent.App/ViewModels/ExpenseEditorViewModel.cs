using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pawscent.App.Services;
using Pawscent.Core.Expenses;
using Pawscent.Core.Profiles;
using Pawscent.Core.Receipts;

namespace Pawscent.App.ViewModels;

/// <summary>
/// The add/edit expense dialog. Categories and receipts can be created from the dialog; they are kept even if the
/// expense is cancelled and can be removed from their own tabs.
/// </summary>
public sealed partial class ExpenseEditorViewModel : ObservableObject
{
    private readonly ProfileDocument _document;
    private readonly IDialogService _dialogs;
    private readonly ReceiptFiles _receiptFiles;
    private readonly Expense? _existing;
    private bool _saveAttempted;

    public ExpenseEditorViewModel(
        ProfileDocument document,
        IDialogService dialogs,
        ReceiptFiles receiptFiles,
        Expense? existing,
        DateOnly date,
        string currency)
    {
        _document = document;
        _dialogs = dialogs;
        _receiptFiles = receiptFiles;
        _existing = existing;
        Date = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue));
        Amount = existing is null ? double.NaN : (double)existing.Amount;
        Currency = currency;
        Description = existing?.Description ?? string.Empty;
        LoadCategories(existing?.CategoryId);
        LoadReceipts(existing?.ReceiptId);
        UpdateValidation();
    }

    public string Title => _existing is null ? "Add expense" : "Edit expense";

    public DateTimeOffset? Date
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateValidation();
        }
    }

    /// <summary>NaN while the amount box is empty.</summary>
    public double Amount
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateValidation();
        }
    }

    public string Currency
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateValidation();
        }
    } = string.Empty;

    public string Description
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateValidation();
        }
    } = string.Empty;

    public ObservableCollection<Category> Categories { get; } = [];

    public Category? SelectedCategory
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateValidation();
        }
    }

    public ObservableCollection<ReceiptRow> Receipts { get; } = [];

    public ReceiptRow? SelectedReceipt
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateValidation();
        }
    }

    /// <summary>Why the new category or receipt could not be added; empty if nothing went wrong.</summary>
    public string AddError
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>Why the expense cannot be saved; shown once the user has tried to save. Empty if it can.</summary>
    public string ErrorText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public bool CanSave
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Creates a category and selects it. Returns false (and sets <see cref="AddError"/>) if the name is invalid.</summary>
    public bool TryAddCategory(string name)
    {
        var error = _document.Categories.Validate(name);
        AddError = error ?? string.Empty;
        if (error is not null)
            return false;
        var category = _document.Categories.Create(name);
        LoadCategories(category.Id);
        return true;
    }

    /// <summary>Creates or updates the expense. Returns false if the values are not valid.</summary>
    public bool TrySave()
    {
        _saveAttempted = true;
        UpdateValidation();
        if (!CanSave || Date is not { } date || SelectedCategory is null || SelectedReceipt is null)
            return false;

        var day = DateOnly.FromDateTime(date.Date);
        var amount = (decimal)Amount;
        try
        {
            if (_existing is null || !_document.Expenses.Contains(_existing.Id))
                _document.Expenses.Create(day, amount, Currency, Description, SelectedCategory.Id, SelectedReceipt.Receipt.Id);
            else
                _document.Expenses.Update(
                    _existing.Id, day, amount, Currency, Description, SelectedCategory.Id, SelectedReceipt.Receipt.Id);
            return true;
        }
        catch (ArgumentException ex)
        {
            ErrorText = ex.Message;
            return false;
        }
    }

    [RelayCommand]
    private async Task AddReceiptAsync()
    {
        var path = await _dialogs.PickReceiptFileAsync();
        if (path is null)
            return;
        try
        {
            var receipt = _receiptFiles.Import(_document, path);
            AddError = string.Empty;
            LoadReceipts(receipt.Id);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            AddError = $"Could not add receipt: {ex.Message}";
        }
    }

    private void LoadCategories(Guid? selectId)
    {
        Categories.Clear();
        foreach (var category in _document.Categories.Items.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
            Categories.Add(category);
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == selectId);
    }

    private void LoadReceipts(Guid? selectId)
    {
        Receipts.Clear();
        foreach (var receipt in _document.Receipts.Items.OrderBy(r => Path.GetFileName(r.FilePath), StringComparer.CurrentCultureIgnoreCase))
            Receipts.Add(new ReceiptRow(receipt, _document.Expenses.ForReceipt(receipt.Id).Count));
        SelectedReceipt = Receipts.FirstOrDefault(r => r.Receipt.Id == selectId);
    }

    private void UpdateValidation()
    {
        string? error;
        if (Date is null)
            error = "Choose a date.";
        else if (double.IsNaN(Amount) || double.IsInfinity(Amount))
            error = "Enter an amount.";
        else if (Math.Abs(Amount) > (double)decimal.MaxValue / 2)
            error = "The amount is too large.";
        else
            error = _document.Expenses.Validate(
                Currency ?? string.Empty,
                Description ?? string.Empty,
                SelectedCategory?.Id ?? Guid.Empty,
                SelectedReceipt?.Receipt.Id ?? Guid.Empty);
        ErrorText = _saveAttempted ? error ?? string.Empty : string.Empty;
        CanSave = error is null;
    }
}
