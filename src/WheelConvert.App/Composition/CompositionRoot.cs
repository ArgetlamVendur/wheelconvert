using System.Windows.Threading;
using WheelConvert.App.Pdf;
using WheelConvert.App.Tray;
using WheelConvert.App.Triggers;
using WheelConvert.App.Wheel;
using WheelConvert.Converters.Image;
using WheelConvert.Converters.Media;
using WheelConvert.Converters.Pdf;
using WheelConvert.Converters.Qr;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Infrastructure;
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
    public static AppHost Build(Dispatcher dispatcher)
    {
        var log = new FileLog(FileLog.DefaultPath);
        log.Info("Starting WheelConvert.");

        var settingsStore = new JsonSettingsStore(JsonSettingsStore.DefaultPath, log);
        var settings = settingsStore.Load();
        AppSettings CurrentSettings() => settings; // TODO(WP-11): replace with a field updated on every save.

        // TODO(WP-11): health checks may load native libraries; build the registry with Task.Run during start-up.
        var pdfRenderer = new PdfPageRenderer();
        var registry = new FormatRegistry(Converters(CurrentSettings), log);
        var queue = new ConversionJobQueue(registry, new SafeOutputWriter(), log, settings.MaxConcurrentJobs);

        var tray = new TrayIcon(dispatcher, CurrentSettings, registry);
        var wheel = new WheelWindow();
        var clipboard = new ClipboardService(dispatcher);
        var coordinator = new DropCoordinator(registry, queue, tray, clipboard, new PageOrderPrompt(pdfRenderer, log), CurrentSettings, dispatcher);
        wheel.TargetsProvider = coordinator.GetTargets;

        return new AppHost(Triggers(settings), wheel, queue, tray, coordinator, log);
    }

    private static IEnumerable<IConverter> Converters(Func<AppSettings> settings) =>
    [
        new ImageConverter(),
        new MediaConverter(() => settings().FfmpegPath),
        new PdfConverter(),
        new QrConverter(),
    ];

    // TODO(WP-11): ADR-006 fallback: if ShiftDragTrigger.Start() returns false, also start HotkeyTrigger and notify once.
    private static IReadOnlyList<ITrigger> Triggers(AppSettings settings) => settings.Trigger switch
    {
        TriggerMode.Hotkey => [new HotkeyTrigger(settings.Hotkey)],
        TriggerMode.Both => [new ShiftDragTrigger(settings.DragThresholdPx), new HotkeyTrigger(settings.Hotkey)],
        _ => [new ShiftDragTrigger(settings.DragThresholdPx)],
    };
}
