namespace WheelConvert.Core.Contracts;

/// <summary>
/// The ONLY way converters create files. Guarantees: never overwrites an existing file, never touches the
/// source, never publishes a half-written file under a real name, and only ever deletes temp files it issued.
/// Converters receive a per-job instance; anything not committed when the job ends is discarded automatically,
/// but converters must still call <see cref="Discard"/> on their own failure paths (CONTRACTS C2 S4).
/// </summary>
public interface IOutputWriter
{
    /// <summary>
    /// Returns a new temp path ".~wc-&lt;32 hex&gt;&lt;ext&gt;" in the source's folder. The file does not exist yet;
    /// the converter creates it. (A leading dot does NOT hide files on Windows; they are short-lived.)
    /// </summary>
    string CreateTempPath(string sourcePath, string outputExtension);

    /// <summary>
    /// Atomically renames a temp path issued by <see cref="CreateTempPath"/> for the same source to
    /// "&lt;stem&gt; (&lt;label&gt;)&lt;ext&gt;" next to the source, adding " 2", " 3"… until the name is free.
    /// </summary>
    /// <param name="label">E.g. "converted", "page 3". Invalid filename characters are removed.</param>
    /// <returns>The final path.</returns>
    /// <exception cref="InvalidOperationException">Path was not issued by this writer, or is in another folder.</exception>
    /// <exception cref="FileNotFoundException">The converter didn't create the temp file.</exception>
    /// <exception cref="IOException">No free name, disk error, or file locked.</exception>
    /// <exception cref="UnauthorizedAccessException">Folder not writable.</exception>
    string Commit(string tempPath, string sourcePath, string outputExtension, string label);

    /// <summary>Deletes a temp file issued by <see cref="CreateTempPath"/>. Refuses any other path. Never throws.</summary>
    bool Discard(string tempPath);
}
