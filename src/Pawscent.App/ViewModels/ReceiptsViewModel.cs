using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pawscent.App.Services;
using Pawscent.Core.Profiles;
using Pawscent.Core.Receipts;

namespace Pawscent.App.ViewModels;

/// <summary>The receipts tab: the profile's receipt files, shown by file name, with add, open and delete.</summary>
public sealed partial class ReceiptsViewModel : ObservableObject
{
    private readonly ProfileDocument _document;
    private readonly IDialogService _dialogs;
    private readonly ReceiptFiles _receiptFiles;

    public ReceiptsViewModel(ProfileDocument document, IDialogService dialogs, ReceiptFiles receiptFiles)
    {
        _document = document;
        _dialogs = dialogs;
        _receiptFiles = receiptFiles;
        Refresh();
    }

    public ObservableCollection<ReceiptRow> Items { get; } = [];

    public ReceiptRow? SelectedItem
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OpenCommand.NotifyCanExecuteChanged();
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public void Refresh()
    {
        var selectedId = SelectedItem?.Receipt.Id;
        Items.Clear();
        foreach (var receipt in _document.Receipts.Items.OrderBy(r => Path.GetFileName(r.FilePath), StringComparer.CurrentCultureIgnoreCase))
            Items.Add(new ReceiptRow(receipt, _document.Expenses.ForReceipt(receipt.Id).Count));
        SelectedItem = Items.FirstOrDefault(r => r.Receipt.Id == selectedId);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var path = await _dialogs.PickReceiptFileAsync();
        if (path is null)
            return;
        try
        {
            var receipt = _receiptFiles.Import(_document, path);
            SelectedItem = Items.FirstOrDefault(r => r.Receipt.Id == receipt.Id);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowMessageAsync("Could not add receipt", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task OpenAsync()
    {
        if (SelectedItem is not { } row)
            return;
        try
        {
            Process.Start(new ProcessStartInfo(row.Receipt.FilePath) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or IOException)
        {
            await _dialogs.ShowMessageAsync("Could not open receipt", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedItem is not { } row)
            return;
        if (row.ExpenseCount > 0)
        {
            await _dialogs.ShowMessageAsync(
                "Receipt in use",
                $"\"{row.FileName}\" is used by {row.ExpenseCount} expense(s). Choose another receipt for them first.");
            return;
        }
        if (!await _dialogs.ConfirmAsync("Delete receipt", $"Remove \"{row.FileName}\" from this profile?", "Delete")
            || !_document.Receipts.Contains(row.Receipt.Id))
        {
            return;
        }

        _document.Receipts.Delete(row.Receipt.Id);
        try
        {
            _receiptFiles.DeleteStoredCopy(_document.Profile, row.Receipt);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowMessageAsync("Could not delete receipt file", ex.Message);
        }
    }

    private bool HasSelection() => SelectedItem is not null;
}
