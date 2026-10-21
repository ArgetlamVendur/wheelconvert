using System.Diagnostics;
using System.Text;

namespace WheelConvert.Core.Infrastructure;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, bool TimedOut);

/// <summary>
/// Runs an external tool (FFmpeg) safely: argument list (no shell, no quoting bugs), no window,
/// bounded captured output (the TAIL is kept), timeout, and the whole process tree is killed on cancel/timeout.
/// Output is decoded as UTF-8, which is right for FFmpeg; other tools may use the OEM code page.
/// </summary>
public static class ProcessRunner
{
    /// <summary>Max characters kept per stream; FFmpeg stderr can be huge.</summary>
    public const int MaxCapturedChars = 64 * 1024;

    /// <exception cref="OperationCanceledException">Cancelled by <paramref name="cancellationToken"/> (process is killed first).</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">The executable could not be started.</exception>
    public static async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onStdErrLine = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var stdout = new BoundedBuffer(MaxCapturedChars);
        var stderr = new BoundedBuffer(MaxCapturedChars);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            stderr.AppendLine(e.Data);
            try
            {
                onStdErrLine?.Invoke(e.Data);
            }
            catch
            {
                // A progress callback must never crash the runner.
            }
        };

        cancellationToken.ThrowIfCancellationRequested();
        process.Start();
        try
        {
            process.StandardInput.Close(); // Tools waiting for input (e.g. FFmpeg's overwrite prompt) get EOF instead of hanging.
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // .NET 8's WaitForExitAsync also waits for redirected streams to reach EOF.
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), TimedOut: false);
        }
        catch (OperationCanceledException)
        {
            KillTree(process);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new ProcessResult(-1, stdout.ToString(), stderr.ToString(), TimedOut: true);
        }
        catch
        {
            KillTree(process); // Never leave an orphaned tool running if setup after Start() failed.
            throw;
        }
    }

    private static void KillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or AggregateException)
        {
            // Already exited or cannot be killed; nothing more to do.
        }
    }

    private sealed class BoundedBuffer(int maxChars)
    {
        private readonly StringBuilder _builder = new();
        private readonly object _gate = new();
        private bool _truncated;

        public void AppendLine(string line)
        {
            lock (_gate)
            {
                if (_builder.Length + line.Length + 1 > maxChars)
                {
                    // Keep the tail: the last lines hold the error message.
                    var overflow = _builder.Length + line.Length + 1 - maxChars;
                    _builder.Remove(0, Math.Min(overflow, _builder.Length));
                    _truncated = true;
                }

                _builder.AppendLine(line);
            }
        }

        public override string ToString()
        {
            lock (_gate)
            {
                return _truncated ? "[…truncated…]" + Environment.NewLine + _builder : _builder.ToString();
            }
        }
    }
}
