using System.Threading;
using System.Windows;
using System.Windows.Threading;
using WheelConvert.App.Composition;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Infrastructure;

// The app exposes no API to other .NET languages.
[assembly: CLSCompliant(false)]

namespace WheelConvert.App;

/// <summary>Entry point. Owned by WP-11. Single instance, global exception logging, then hands over to the composition root.</summary>
public sealed partial class App : Application, IDisposable
{
    private const string MutexName = @"Local\WheelConvert.SingleInstance";
    private Mutex? _singleInstance;
    private FileLog? _log;
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown(0);
            return;
        }

        _log = new FileLog(FileLog.DefaultPath);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _ = StartAsync(_log);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Blocking here is acceptable: the app is exiting, and AppHost does its UI-thread work before its first await.
        _host?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Dispose();
        base.OnExit(e);
    }

    /// <summary>Releases the single-instance mutex. Must run on the UI thread, which acquired it.</summary>
    public void Dispose()
    {
        if (_singleInstance is not null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
            _singleInstance = null;
        }
    }

    private async Task StartAsync(FileLog log)
    {
        try
        {
            _host = await CompositionRoot.BuildAsync(Dispatcher, log);
            _host.Start();
        }
        catch (Exception ex)
        {
            // There is no tray icon yet to notify through.
            log.Error("Start-up failed.", ex);
            MessageBox.Show($"WheelConvert couldn't start. Details are in {FileLog.DefaultPath}", "WheelConvert", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    // WP-11 req. 2: a bug in UI code is logged and reported once, and doesn't crash the app.
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log?.Error("Unhandled exception on the UI thread.", e.Exception);
        _host?.Notifier.Notify(NotificationKind.Error, "WheelConvert", "Something went wrong. Details are in the log.");
        e.Handled = true;
    }

    // The process is about to end; all that can be done is to record why.
    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        _log?.Error($"Unhandled exception (terminating: {e.IsTerminating}).", e.ExceptionObject as Exception);

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _log?.Error("Unobserved task exception.", e.Exception);
        e.SetObserved();
    }
}
