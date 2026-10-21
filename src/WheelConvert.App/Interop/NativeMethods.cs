namespace WheelConvert.App.Interop;

/// <summary>
/// ALL Win32 P/Invoke declarations live here (or in sibling files in this folder).
/// Rules (AGENTS.md §4, docs/checklists/review.md):
///  - Every function, struct and constant has a comment with its learn.microsoft.com URL.
///  - Use LibraryImport/DllImport with SetLastError = true where the doc says GetLastError applies.
///  - Pointer-sized fields are nint/nuint (x64). Structs use [StructLayout(LayoutKind.Sequential)].
///  - Group by owner: hook members (WP-02), hotkey (WP-03), window styles (WP-04). Owners add only their own.
/// </summary>
internal static partial class NativeMethods
{
}
