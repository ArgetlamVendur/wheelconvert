using System.Threading;
using System.Windows;
using WheelConvert.App.Composition;

// The app exposes no API to other .NET languages.
[assembly: CLSCompliant(false)]

namespace WheelConvert.App;

/// <summary>Entry point. Owned by WP-11. Single instance, then hands over to the composition root.</summary>
public sealed partial class App : Application, IDisposable
{
    private const string MutexName = @"Local\WheelConvert.SingleInstance";
    private Mutex? _singleInstance;
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

        _host = CompositionRoot.Build(Dispatcher);
        _host.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Blocking here is acceptable: the app is exiting and the queue cancels running jobs.
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
}
