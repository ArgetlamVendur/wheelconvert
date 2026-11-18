using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Composition;

/// <summary>Placeholder until WP-19 adds the real resize dialog. Always "cancelled", and no converter offers image.resize yet.</summary>
public sealed class NotImplementedResizePrompt : IResizePrompt
{
    public Task<ResizeChoice?> PromptAsync(IReadOnlyList<string> imagePaths, CancellationToken cancellationToken) =>
        Task.FromResult<ResizeChoice?>(null);
}
