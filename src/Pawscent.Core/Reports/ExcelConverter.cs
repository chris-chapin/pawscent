using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Pawscent.Core.Reports;

/// <summary>
/// Converts spreadsheets to PDF with an installed Microsoft Excel, through COM automation: a hidden Excel of its own
/// opens the file read-only with macros disabled and exports every sheet, honoring the workbook's print settings.
/// Excel is optional.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ExcelConverter : IOfficeApp
{
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private const string ProgId = "Excel.Application";

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xlsm", ".xls", ".ods", ".csv",
    };

    public string Name => "Microsoft Excel";

    public bool IsInstalled => Type.GetTypeFromProgID(ProgId) is not null;

    public bool Converts(string extension) => Extensions.Contains(extension);

    public string ConvertToPdf(string sourcePath, string workFolder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(workFolder);
        var output = Path.Combine(workFolder, "receipt.pdf");

        new ExcelRun(Path.GetFullPath(sourcePath), output).Run(cancellationToken);

        if (!File.Exists(output))
            throw new ReceiptConversionException("Microsoft Excel could not convert this file to PDF.");
        return output;
    }

    // One hidden Excel per conversion, driven from a thread of its own: Excel's COM calls block, so the caller waits
    // with a timeout and ends Excel's process to stop it.
    private sealed class ExcelRun(string source, string output)
    {
        private const int MsoAutomationSecurityForceDisable = 3;
        private const int XlTypePdf = 0;
        private const int XlQualityStandard = 0;
        private const int RpcCallRejected = unchecked((int)0x80010001);
        private const int RpcServerCallRetryLater = unchecked((int)0x8001010A);

        // A password Excel never asks for: a password-protected file then fails to open instead of showing a prompt.
        private const string NoPassword = "pawscent-no-password";

        private static readonly TimeSpan QuitTimeout = TimeSpan.FromSeconds(10);

        private readonly Lock _lock = new();
        private readonly ManualResetEventSlim _done = new();
        private int _processId;
        private bool _stopped;
        private ReceiptConversionException? _failure;

        public void Run(CancellationToken cancellationToken)
        {
            var thread = new Thread(Work) { IsBackground = true, Name = "Excel conversion" };
            thread.Start();

            bool finished;
            try
            {
                finished = _done.Wait(Timeout, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Stop();
                throw;
            }
            if (!finished)
            {
                Stop();
                throw new ReceiptConversionException("Microsoft Excel took too long to convert this file.");
            }

            EndIfLingering();
            if (_failure is not null)
                throw _failure;
        }

        private void Work()
        {
            dynamic? excel = null;
            dynamic? workbooks = null;
            dynamic? workbook = null;
            try
            {
                try
                {
                    excel = Activator.CreateInstance(Type.GetTypeFromProgID(ProgId, throwOnError: true)!);
                }
                catch (Exception ex)
                {
                    _failure = new ReceiptConversionException($"Microsoft Excel could not be started: {ex.Message}");
                    return;
                }
                Track(new IntPtr(Convert.ToInt64(Retry(() => excel!.Hwnd), CultureInfo.InvariantCulture)));

                Retry(() => excel!.DisplayAlerts = false);
                Retry(() => excel!.ScreenUpdating = false);
                Retry(() => excel!.EnableEvents = false);
                Retry(() => excel!.AskToUpdateLinks = false);
                Retry(() => excel!.AutomationSecurity = MsoAutomationSecurityForceDisable);
                workbooks = Retry(() => excel!.Workbooks);
                // Open(Filename, UpdateLinks, ReadOnly, Format, Password, WriteResPassword, IgnoreReadOnlyRecommended,
                //      Origin, Delimiter, Editable, Notify, Converter, AddToMru)
                workbook = Retry(() => workbooks!.Open(
                    source, 0, true, Type.Missing, NoPassword, Type.Missing, true,
                    Type.Missing, Type.Missing, false, false, Type.Missing, false));
                // ExportAsFixedFormat(Type, Filename, Quality, IncludeDocProperties, IgnorePrintAreas, From, To,
                //                     OpenAfterPublish)
                Retry(() =>
                {
                    workbook!.ExportAsFixedFormat(
                        XlTypePdf, output, XlQualityStandard, false, false, Type.Missing, Type.Missing, false);
                });
            }
            // An exception left on this thread would end the app. COM errors come as many exception types.
            catch (Exception)
            {
                _failure = new ReceiptConversionException("Microsoft Excel could not convert this file to PDF.");
            }
            finally
            {
                Quit(excel, workbooks, workbook);
                _done.Set();
            }
        }

        private void Quit(dynamic? excel, dynamic? workbooks, dynamic? workbook)
        {
            if (workbook is not null)
                Ignore(() => { workbook.Close(false); });
            if (excel is not null)
            {
                // A file the user opened while this Excel ran may have landed in it: hand that Excel over instead.
                if (workbooks is not null && Ignore(() => workbooks.Count) is int count && count > 0)
                    HandOver(excel);
                else
                    Ignore(() => { excel.Quit(); });
            }
            foreach (var comObject in new object?[] { workbook, workbooks, excel })
            {
                if (comObject is not null)
                    Ignore(() => Marshal.FinalReleaseComObject(comObject));
            }
        }

        private void HandOver(dynamic excel)
        {
            lock (_lock)
                _processId = 0;
            Ignore(() => excel.DisplayAlerts = true);
            Ignore(() => excel.ScreenUpdating = true);
            Ignore(() => excel.EnableEvents = true);
            Ignore(() => excel.Visible = true);
            Ignore(() => excel.UserControl = true);
        }

        private void Retry(Action call) => Retry(() =>
        {
            call();
            return null;
        });

        // A busy Excel turns calls away for a moment instead of queueing them.
        private object? Retry(Func<object?> call)
        {
            while (true)
            {
                try
                {
                    return call();
                }
                catch (COMException ex) when (ex.HResult is RpcCallRejected or RpcServerCallRetryLater && !IsStopped())
                {
                    Thread.Sleep(100);
                }
            }
        }

        private static void Ignore(Action call) => Ignore(() =>
        {
            call();
            return null;
        });

        // While Excel quits it may already be gone; nothing is left to do then.
        private static object? Ignore(Func<object?> call)
        {
            try
            {
                return call();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Track(IntPtr window)
        {
            _ = GetWindowThreadProcessId(window, out var processId);
            bool stopped;
            lock (_lock)
            {
                _processId = (int)processId;
                stopped = _stopped;
            }
            if (stopped)
                Kill((int)processId);
        }

        private bool IsStopped()
        {
            lock (_lock)
                return _stopped;
        }

        private void Stop()
        {
            int processId;
            lock (_lock)
            {
                _stopped = true;
                processId = _processId;
            }
            if (processId != 0)
                Kill(processId);
        }

        // Excel normally exits soon after quitting; end it if it doesn't.
        private void EndIfLingering()
        {
            int processId;
            lock (_lock)
                processId = _processId;
            if (processId == 0)
                return;
            try
            {
                using var process = Process.GetProcessById(processId);
                if (!process.WaitForExit(QuitTimeout))
                    process.Kill();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                // Already exited.
            }
        }

        private static void Kill(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                process.Kill();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                // Already exited.
            }
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    }
}
