using System.Windows.Threading;
using WheelConvert.App.Pdf;
using WheelConvert.App.Tray;
using WheelConvert.App.Wheel;
using WheelConvert.Converters.Image;
using WheelConvert.Converters.Media;
using WheelConvert.Converters.Pdf;
using WheelConvert.Converters.Qr;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Jobs;
using WheelConvert.Core.Output;
using WheelConvert.Core.Settings;

namespace WheelConvert.App.Composition;

/// <summary>
/// The ONLY place that knows every module. Owned by WP-11. Adding a converter = one line in Converters().
/// Constructors called here are frozen; changing one needs WP-11's agreement.
/// </summary>
public static class CompositionRoot
{
    /// <summary>
    /// Call on the UI thread. Settings and converter health checks (which may load native libraries such as Magick
    /// and PDFium) run on a worker thread. The tray, wheel and triggers are created only once the registry is ready,
    /// so the wheel can never open before its targets are known (the frozen TrayIcon constructor needs the registry too).
    /// </summary>
    public static async Task<AppHost> BuildAsync(Dispatcher dispatcher, ILog log)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(log);
        log.Info("Starting WheelConvert.");

        var (settings, registry, pdfRenderer) = await Task.Run(() =>
        {
            var store = new JsonSettingsStore(JsonSettingsStore.DefaultPath, log);
            var holder = new SettingsHolder(store, store.Load(), log);
            var renderer = new PdfPageRenderer();
            return (holder, new FormatRegistry(Converters(() => holder.Current), log), renderer);
        });

        AppSettings CurrentSettings() => settings.Current;
        var queue = new ConversionJobQueue(registry, new SafeOutputWriter(), log, settings.Current.MaxConcurrentJobs);
        var tray = new TrayIcon(dispatcher, CurrentSettings, registry);
        var wheel = new WheelWindow();
        var clipboard = new ClipboardService(dispatcher);
        var coordinator = new DropCoordinator(registry, queue, tray, clipboard, new PageOrderPrompt(pdfRenderer, log), CurrentSettings, dispatcher);
        wheel.TargetsProvider = coordinator.GetTargets;
        var triggers = new TriggerManager(wheel, tray, CurrentSettings, log, dispatcher);
        var recentFolders = new RecentFolders(RecentFolders.DefaultPath, log);

        return new AppHost(triggers, wheel, queue, tray, coordinator, registry, settings, recentFolders, log);
    }

    private static IEnumerable<IConverter> Converters(Func<AppSettings> settings) =>
    [
        new ImageConverter(),
        new MediaConverter(() => settings().FfmpegPath),
        new PdfConverter(),
        new QrConverter(),
    ];
}
