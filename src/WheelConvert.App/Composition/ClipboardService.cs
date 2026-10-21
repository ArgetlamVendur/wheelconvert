using System.Runtime.InteropServices;
using System.Windows.Threading;
using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Composition;

/// <summary>WP-11. Puts text on the clipboard from any thread (WPF clipboard needs the STA UI thread). Never throws.</summary>
public sealed class ClipboardService(Dispatcher dispatcher) : IClipboardService
{
    public async Task<bool> SetTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var ok = await dispatcher.InvokeAsync(() =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    return true;
                }
                catch (COMException)
                {
                    return false; // Clipboard briefly locked by another app.
                }
                catch (ExternalException)
                {
                    return false;
                }
            });

            if (ok)
            {
                return true;
            }

            await Task.Delay(100 * attempt).ConfigureAwait(false);
        }

        return false;
    }
}
