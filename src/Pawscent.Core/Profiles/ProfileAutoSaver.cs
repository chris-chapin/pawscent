namespace Pawscent.Core.Profiles;

/// <summary>
/// Saves a <see cref="ProfileDocument"/> a short delay after its last change. Each change restarts the delay, so a burst
/// of edits produces one save.
/// </summary>
public sealed class ProfileAutoSaver : IDisposable
{
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(3);

    private readonly ProfileDocument _document;
    private readonly IProfileService _profiles;
    private readonly TimeSpan _delay;
    private readonly SynchronizationContext? _context;
    private readonly ITimer _timer;
    private bool _disposed;

    /// <param name="context">
    /// Where timed saves run; pass the UI context so saves never overlap edits. Null runs them on the timer's thread.
    /// </param>
    public ProfileAutoSaver(
        ProfileDocument document,
        IProfileService profiles,
        TimeSpan delay,
        TimeProvider timeProvider,
        SynchronizationContext? context)
    {
        _document = document;
        _profiles = profiles;
        _delay = delay;
        _context = context;
        _timer = timeProvider.CreateTimer(_ => OnTimerElapsed(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _document.Changed += OnDocumentChanged;
    }

    public ProfileDocument Document => _document;

    public bool HasUnsavedChanges { get; private set; }

    /// <summary>Raised when a timed save fails. The changes stay unsaved and are retried on the next change.</summary>
    public event EventHandler<Exception>? SaveFailed;

    /// <summary>Saves immediately if there are unsaved changes and cancels any pending timed save.</summary>
    public void SaveNow()
    {
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        if (!HasUnsavedChanges)
            return;
        _profiles.Save(_document);
        HasUnsavedChanges = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _document.Changed -= OnDocumentChanged;
        _timer.Dispose();
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        HasUnsavedChanges = true;
        _timer.Change(_delay, Timeout.InfiniteTimeSpan);
    }

    private void OnTimerElapsed()
    {
        if (_context is null)
            SaveFromTimer();
        else
            _context.Post(_ => SaveFromTimer(), null);
    }

    private void SaveFromTimer()
    {
        if (_disposed)
            return;
        try
        {
            SaveNow();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveFailed?.Invoke(this, ex);
        }
    }
}
