using System.Runtime.InteropServices;

namespace WheelConvert.App.Interop;

/// <summary>Shell item lists, used to select a file in Explorer without building a command line.</summary>
internal static partial class NativeMethods
{
    /// <summary>
    /// Converts a path to an absolute item ID list (PIDL). Returns an HRESULT (0 = S_OK); on error the PIDL is NULL.
    /// pbc is always NULL (the doc: "normally NULL"). The doc says to call it from a background thread.
    /// The PIDL must be released with <see cref="ILFree"/>.
    /// </summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shparsedisplayname</remarks>
    [LibraryImport("shell32.dll", EntryPoint = "SHParseDisplayName", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int SHParseDisplayName(string pszName, nint pbc, out nint ppidl, uint sfgaoIn, out uint psfgaoOut);

    /// <summary>
    /// Opens an Explorer window with the item selected. With cidl = 0, pidlFolder is the full PIDL of the single item
    /// to select (the doc: "opens the parent folder and selects that item"); apidl is then NULL. Returns an HRESULT.
    /// CoInitialize(Ex) must have run on the calling thread or the call fails.
    /// </summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shopenfolderandselectitems</remarks>
    [LibraryImport("shell32.dll", EntryPoint = "SHOpenFolderAndSelectItems")]
    internal static partial int SHOpenFolderAndSelectItems(nint pidlFolder, uint cidl, nint apidl, uint dwFlags);

    /// <summary>Frees a PIDL returned by SHParseDisplayName. Safe to call with NULL.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-ilfree</remarks>
    [LibraryImport("shell32.dll", EntryPoint = "ILFree")]
    internal static partial void ILFree(nint pidl);
}
