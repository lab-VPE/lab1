using System;
using System.Diagnostics;
using Avalonia.Threading;

namespace UI.Services;

/// <summary>ITicker на базе Avalonia DispatcherTimer (~60 кадров/с).</summary>
public sealed class DispatcherTicker : ITicker
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = new();

    public event Action<double>? Tick;

    public DispatcherTicker()
    {
        _timer.Tick += (_, _) =>
        {
            var dt = _clock.Elapsed.TotalSeconds;
            _clock.Restart();
            Tick?.Invoke(dt);
        };
    }

    public void Start()
    {
        _clock.Restart();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _clock.Stop();
    }
}
