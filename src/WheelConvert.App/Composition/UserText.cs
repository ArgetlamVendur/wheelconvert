namespace WheelConvert.App.Composition;

/// <summary>Helpers for text shown to the user.</summary>
internal static class UserText
{
    public const string Title = "WheelConvert";
    private const int MaxLength = 120;

    /// <summary>User-facing text is at most 120 characters (AGENTS §6); long file names are cut, not the app.</summary>
    public static string Fit(string message) =>
        message.Length <= MaxLength ? message : string.Concat(message.AsSpan(0, MaxLength - 1), "…");
}
