using System.Security.Cryptography;
using WheelConvert.Core.Infrastructure;
using WheelConvert.Core.Output;
using Xunit;

namespace WheelConvert.Converters.Media.Tests;

/// <summary>
/// A fresh folder under the system temp directory, deleted afterwards. All test media is generated here;
/// no test ever reads the user's scratch folder (AGENTS.md §4.11).
/// </summary>
internal sealed class TestFolder : IDisposable
{
    public TestFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wc-media-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public string CreateFile(string name, byte[] content)
    {
        var path = File(name);
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    public string[] TempFiles() => Directory.GetFiles(Path).Where(SafeOutputWriter.IsTempFile).ToArray();

    public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(path)));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover folder in %TEMP% is harmless and must not fail the test run.
        }
    }
}

/// <summary>The real FFmpeg on this machine, if any. CI runners don't have it.</summary>
internal static class RealFfmpeg
{
    public const string SkipReason =
        "FFmpeg not installed (not on PATH or in the winget Links folder): skipped. Install: winget install Gyan.FFmpeg";

    public static string? Path { get; } = ToolLocator.Find("ffmpeg");

    /// <summary>Runs FFmpeg to make test media inside a test folder. Fails the test if FFmpeg fails.</summary>
    public static async Task GenerateAsync(params string[] args)
    {
        var result = await ProcessRunner.RunAsync(
            Path ?? throw new InvalidOperationException(SkipReason),
            ["-hide_banner", "-nostdin", "-loglevel", "error", "-n", .. args],
            TimeSpan.FromMinutes(2),
            CancellationToken.None);
        Assert.True(result.ExitCode == 0, "Generating test media failed: " + result.StdErr);
    }

    /// <summary>True when FFmpeg can decode the whole file without errors.</summary>
    public static async Task<bool> DecodesCleanlyAsync(string file)
    {
        var result = await ProcessRunner.RunAsync(
            Path ?? throw new InvalidOperationException(SkipReason),
            ["-hide_banner", "-nostdin", "-v", "error", "-i", file, "-f", "null", "-"],
            TimeSpan.FromMinutes(2),
            CancellationToken.None);
        return result.ExitCode == 0 && string.IsNullOrWhiteSpace(result.StdErr);
    }
}

/// <summary>A [Fact] that is reported as SKIPPED (with the reason in the test output) when FFmpeg isn't installed.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute()
    {
        if (RealFfmpeg.Path is null)
        {
            Skip = RealFfmpeg.SkipReason;
        }
    }
}

/// <summary>A [Theory] that is reported as SKIPPED when FFmpeg isn't installed.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class FfmpegTheoryAttribute : TheoryAttribute
{
    public FfmpegTheoryAttribute()
    {
        if (RealFfmpeg.Path is null)
        {
            Skip = RealFfmpeg.SkipReason;
        }
    }
}

/// <summary>Stands in for ProcessRunner: records the call and plays a scripted FFmpeg run.</summary>
internal sealed class FakeRunner
{
    public string? Executable { get; private set; }

    public IReadOnlyList<string>? Arguments { get; private set; }

    public TimeSpan Timeout { get; private set; }

    public int Calls { get; private set; }

    /// <summary>Bytes written to the output path (last argument) before "exiting"; null = write nothing.</summary>
    public byte[]? WriteOutput { get; init; } = [1, 2, 3];

    public string[] StdErrLines { get; init; } = [];

    public ProcessResult Result { get; init; } = new(0, string.Empty, string.Empty, TimedOut: false);

    /// <summary>When set, the run blocks (after writing output) until the token fires, like a long conversion.</summary>
    public bool BlockUntilCancelled { get; init; }

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Exception? Throw { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Must match ProcessRunner.RunAsync's parameter order (ToolRunner delegate).")]
    public async Task<ProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onStdErrLine)
    {
        Calls++;
        Executable = executable;
        Arguments = arguments;
        Timeout = timeout;

        if (Throw is not null)
        {
            throw Throw;
        }

        if (WriteOutput is not null)
        {
            await System.IO.File.WriteAllBytesAsync(arguments[^1], WriteOutput, CancellationToken.None);
        }

        foreach (var line in StdErrLines)
        {
            onStdErrLine?.Invoke(line);
        }

        Started.TrySetResult();
        if (BlockUntilCancelled)
        {
            await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
        }

        return Result;
    }
}

/// <summary>Collects progress reports synchronously (<see cref="Progress{T}"/> would post them asynchronously).</summary>
internal sealed class ProgressLog : IProgress<double>
{
    private readonly List<double> _values = [];

    public IReadOnlyList<double> Values
    {
        get
        {
            lock (_values)
            {
                return _values.ToArray();
            }
        }
    }

    public void Report(double value)
    {
        lock (_values)
        {
            _values.Add(value);
        }
    }
}

/// <summary>Cancels synchronously the first time progress is strictly between 0 and 1 (the conversion is mid-way).</summary>
internal sealed class CancelMidway(CancellationTokenSource cts) : IProgress<double>
{
    public void Report(double value)
    {
        if (value > 0 && value < 1)
        {
            cts.Cancel();
        }
    }
}
