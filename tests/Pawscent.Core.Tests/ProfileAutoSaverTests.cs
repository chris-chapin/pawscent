using Microsoft.Extensions.Time.Testing;
using Pawscent.Core.Profiles;
using Xunit;

namespace Pawscent.Core.Tests;

public sealed class ProfileAutoSaverTests : IDisposable
{
    private static readonly DateOnly Date = new(2026, 10, 21);

    private readonly FakeTimeProvider _time = new();
    private readonly CountingProfileService _profiles = new();
    private readonly ProfileDocument _document = new(new Profile("Home", DateTimeOffset.UtcNow, 1) { FolderPath = "x" });
    private readonly ProfileAutoSaver _saver;

    public ProfileAutoSaverTests() =>
        _saver = new ProfileAutoSaver(_document, _profiles, ProfileAutoSaver.DefaultDelay, _time, context: null);

    public void Dispose() => _saver.Dispose();

    [Fact]
    public void DefaultDelay_IsThreeSeconds() => Assert.Equal(TimeSpan.FromSeconds(3), ProfileAutoSaver.DefaultDelay);

    [Fact]
    public void NoChanges_NeverSaves()
    {
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(0, _profiles.SaveCount);
        Assert.False(_saver.HasUnsavedChanges);
    }

    [Fact]
    public void Change_SavesAfterDelay()
    {
        _document.Categories.Create("Vet");
        Assert.True(_saver.HasUnsavedChanges);

        _time.Advance(TimeSpan.FromSeconds(2.9));
        Assert.Equal(0, _profiles.SaveCount);

        _time.Advance(TimeSpan.FromSeconds(0.1));
        Assert.Equal(1, _profiles.SaveCount);
        Assert.False(_saver.HasUnsavedChanges);
    }

    [Fact]
    public void EachChange_RestartsDelay()
    {
        var vet = _document.Categories.Create("Vet");
        _time.Advance(TimeSpan.FromSeconds(2));
        var receipt = _document.Receipts.Create(@"C:\r.pdf");
        _time.Advance(TimeSpan.FromSeconds(2));
        _document.Expenses.Create(Date, 1m, "USD", "Checkup", vet.Id, receipt.Id);
        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0, _profiles.SaveCount);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, _profiles.SaveCount);
    }

    [Fact]
    public void UpdatesAndDeletes_TriggerSave()
    {
        var vet = _document.Categories.Create("Vet");
        var receipt = _document.Receipts.Create(@"C:\r.pdf");
        var expense = _document.Expenses.Create(Date, 1m, "USD", "Checkup", vet.Id, receipt.Id);
        _time.Advance(ProfileAutoSaver.DefaultDelay);
        Assert.Equal(1, _profiles.SaveCount);

        Action[] edits =
        [
            () => _document.Expenses.Update(expense.Id, Date, 2m, "USD", "Checkup", vet.Id, receipt.Id),
            () => _document.Expenses.Delete(expense.Id),
            () => _document.Receipts.Update(receipt.Id, @"C:\s.pdf"),
            () => _document.Receipts.Delete(receipt.Id),
            () => _document.Categories.Update(vet.Id, "Doctor"),
            () => _document.Categories.Delete(vet.Id),
        ];
        foreach (var edit in edits)
        {
            var before = _profiles.SaveCount;
            edit();
            _time.Advance(ProfileAutoSaver.DefaultDelay);
            Assert.Equal(before + 1, _profiles.SaveCount);
        }
    }

    [Fact]
    public void FailedEdit_DoesNotTriggerSave()
    {
        _document.Categories.Create("Vet");
        _time.Advance(ProfileAutoSaver.DefaultDelay);

        Assert.Throws<ArgumentException>(() => _document.Categories.Create("vet"));
        _time.Advance(ProfileAutoSaver.DefaultDelay);

        Assert.Equal(1, _profiles.SaveCount);
    }

    [Fact]
    public void SaveNow_SavesImmediatelyAndCancelsTimedSave()
    {
        _document.Categories.Create("Vet");

        _saver.SaveNow();
        _time.Advance(ProfileAutoSaver.DefaultDelay);

        Assert.Equal(1, _profiles.SaveCount);
        Assert.False(_saver.HasUnsavedChanges);
    }

    [Fact]
    public void SaveNow_WithoutChanges_DoesNothing()
    {
        _saver.SaveNow();

        Assert.Equal(0, _profiles.SaveCount);
    }

    [Fact]
    public void FailedTimedSave_RaisesSaveFailedAndKeepsChangesUnsaved()
    {
        Exception? reported = null;
        _saver.SaveFailed += (_, ex) => reported = ex;
        _profiles.FailWith = new IOException("disk full");

        _document.Categories.Create("Vet");
        _time.Advance(ProfileAutoSaver.DefaultDelay);

        Assert.Same(_profiles.FailWith, reported);
        Assert.True(_saver.HasUnsavedChanges);

        _profiles.FailWith = null;
        _saver.SaveNow();
        Assert.Equal(1, _profiles.SaveCount);
    }

    [Fact]
    public void Dispose_StopsPendingSave()
    {
        _document.Categories.Create("Vet");

        _saver.Dispose();
        _time.Advance(ProfileAutoSaver.DefaultDelay);
        _document.Categories.Create("Food");
        _time.Advance(ProfileAutoSaver.DefaultDelay);

        Assert.Equal(0, _profiles.SaveCount);
    }

    [Fact]
    public void TimedSave_RunsOnSynchronizationContext()
    {
        var context = new QueueingContext();
        using var saver = new ProfileAutoSaver(
            new ProfileDocument(_document.Profile), _profiles, ProfileAutoSaver.DefaultDelay, _time, context);

        saver.Document.Categories.Create("Vet");
        _time.Advance(ProfileAutoSaver.DefaultDelay);
        Assert.Equal(0, _profiles.SaveCount);

        context.RunAll();
        Assert.Equal(1, _profiles.SaveCount);
    }

    private sealed class QueueingContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

        public void RunAll()
        {
            while (_queue.TryDequeue(out var item))
                item.Callback(item.State);
        }
    }

    private sealed class CountingProfileService : IProfileService
    {
        public int SaveCount { get; private set; }

        public Exception? FailWith { get; set; }

        public void Save(ProfileDocument document)
        {
            if (FailWith is not null)
                throw FailWith;
            SaveCount++;
        }

        public IReadOnlyList<Profile> List() => throw new NotSupportedException();

        public Profile Create(string name) => throw new NotSupportedException();

        public Profile Load(string name) => throw new NotSupportedException();

        public ProfileDocument Open(string name) => throw new NotSupportedException();

        public string? Validate(string name) => throw new NotSupportedException();
    }
}
