using WheelConvert.Core.Contracts;

namespace WheelConvert.Core.Tests;

public sealed class NullLog : ILog
{
    public List<string> Lines { get; } = [];

    public void Info(string message) => Add("INFO " + message);

    public void Warn(string message, Exception? exception = null) => Add("WARN " + message);

    public void Error(string message, Exception? exception = null) => Add("ERROR " + message);

    private void Add(string line)
    {
        lock (Lines)
        {
            Lines.Add(line);
        }
    }
}

/// <summary>Configurable converter for registry/queue tests. Writes "converted" into a committed output.</summary>
public sealed class FakeConverter(string id, params string[] targetIds) : IConverter
{
    public string Id { get; } = id;

    public IReadOnlyCollection<string> SupportedTargetIds { get; } = targetIds;

    public ConverterHealth Health { get; set; } = ConverterHealth.Ready;

    public Func<string, string, bool> CanConvertFunc { get; set; } = (_, _) => true;

    public Func<ConversionRequest, IOutputWriter, CancellationToken, Task<ConversionResult>>? Behaviour { get; set; }

    public bool CanConvert(string sourceExtension, string targetId) => CanConvertFunc(sourceExtension, targetId);

    public ConverterHealth CheckHealth() => Health;

    public Task<ConversionResult> ConvertAsync(ConversionRequest request, IOutputWriter output, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (Behaviour is not null)
        {
            return Behaviour(request, output, cancellationToken);
        }

        var ext = request.Target.OutputExtension!;
        var temp = output.CreateTempPath(request.SourcePath, ext);
        File.WriteAllText(temp, "converted");
        return Task.FromResult(ConversionResult.Ok(output.Commit(temp, request.SourcePath, ext, "converted")));
    }
}
