namespace WheelConvert.App.Composition;

/// <summary>Result of <see cref="SettingsHolder.Update"/>.</summary>
public enum SettingsSaveOutcome
{
    Saved,

    /// <summary>The store refused to save because the existing file couldn't be read (it is protected from overwrite).</summary>
    Locked,

    /// <summary>Disk error or folder not writable.</summary>
    Failed,
}
