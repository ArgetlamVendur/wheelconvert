using System.IO;
using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Interop;

/// <summary>
/// Shows a file or folder selected in Explorer through the Shell API. The path is only ever passed as a path,
/// never as a command line, so commas, quotes or leading dashes in a file name cannot change what Explorer does.
/// </summary>
public static class ShellReveal
{
    /// <summary>
    /// Opens Explorer with the item selected. Runs on its own STA thread: the Shell API needs COM initialised
    /// (a .NET STA thread does that on start) and the doc asks for SHParseDisplayName to run off the UI thread.
    /// Returns false (and logs) if the path is not absolute, no longer exists, or the Shell refuses.
    /// </summary>
    public static Task<bool> RevealAsync(string path, ILog? log)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(Reveal(path, log));
            }
            catch (Exception ex)
            {
                log?.Warn("Reveal in Explorer failed.", ex);
                completion.SetResult(false);
            }
        })
        {
            IsBackground = true,
            Name = "WheelConvert reveal",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    /// <summary>True if the Shell can turn the path into an item (frees the item at once; opens nothing).</summary>
    public static bool CanResolve(string path)
    {
        if (!Resolve(path, out var pidl))
        {
            return false;
        }

        NativeMethods.ILFree(pidl);
        return true;
    }

    private static bool Reveal(string path, ILog? log)
    {
        if (!Path.IsPathFullyQualified(path) || !(File.Exists(path) || Directory.Exists(path)))
        {
            log?.Info("Reveal skipped: the file no longer exists.");
            return false;
        }

        if (!Resolve(path, out var pidl))
        {
            log?.Warn("Reveal failed: the Shell could not resolve the path.");
            return false;
        }

        try
        {
            var hr = NativeMethods.SHOpenFolderAndSelectItems(pidl, 0, 0, 0);
            if (hr < 0)
            {
                log?.Warn($"Reveal failed: SHOpenFolderAndSelectItems returned 0x{hr:X8}.");
                return false;
            }

            return true;
        }
        finally
        {
            NativeMethods.ILFree(pidl);
        }
    }

    private static bool Resolve(string path, out nint pidl)
    {
        var hr = NativeMethods.SHParseDisplayName(path, 0, out pidl, 0, out _);
        if (hr >= 0 && pidl != 0)
        {
            return true;
        }

        if (pidl != 0)
        {
            NativeMethods.ILFree(pidl);
        }

        pidl = 0;
        return false;
    }
}
