using System.Diagnostics;

namespace WheelConvert.Core.Jobs;

/// <summary>
/// IProgress that forwards at most every 250 ms and only on a ≥ 1 % change; always forwards 1.0.
/// Values are clamped to 0..1; NaN is ignored. Synchronous (no SynchronizationContext capture).
/// </summary>
internal sealed class ThrottledProgress(Action<double> forward) : IProgress<double>
{
    private static readonly long MinTicks = Stopwatch.Frequency / 4;
    private readonly object _gate = new();
    private long _lastTicks = long.MinValue / 2;
    private double _last = -1;

    public void Report(double value)
    {
        if (double.IsNaN(value))
        {
            return;
        }

        var clamped = Math.Clamp(value, 0, 1);
        lock (_gate)
        {
            var now = Stopwatch.GetTimestamp();
            var isFinal = clamped >= 1.0 && _last < 1.0;
            if (!isFinal && (now - _lastTicks < MinTicks || Math.Abs(clamped - _last) < 0.01))
            {
                return;
            }

            _lastTicks = now;
            _last = clamped;

            // Inside the lock so concurrent reports can't be delivered out of order. Subscribers only marshal.
            forward(clamped);
        }
    }
}
