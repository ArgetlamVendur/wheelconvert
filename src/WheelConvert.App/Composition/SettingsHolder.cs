using System.IO;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Settings;

namespace WheelConvert.App.Composition;

/// <summary>
/// WP-11 req. 9: the single current <see cref="AppSettings"/>. Every module reads it through <see cref="Current"/>
/// (passed around as <c>Func&lt;AppSettings&gt;</c>), so a change is visible everywhere without a restart.
/// </summary>
public sealed class SettingsHolder(JsonSettingsStore store, AppSettings initial, ILog log)
{
    private volatile AppSettings _current = initial;

    public AppSettings Current => _current;

    /// <summary>
    /// Applies <paramref name="updated"/> in memory (always, so the change works for this session) and tries to save it.
    /// </summary>
    public SettingsSaveOutcome Update(AppSettings updated)
    {
        ArgumentNullException.ThrowIfNull(updated);
        _current = updated.Normalize();
        try
        {
            return store.Save(_current) ? SettingsSaveOutcome.Saved : SettingsSaveOutcome.Locked;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Warn("Could not save settings.", ex);
            return SettingsSaveOutcome.Failed;
        }
    }
}
