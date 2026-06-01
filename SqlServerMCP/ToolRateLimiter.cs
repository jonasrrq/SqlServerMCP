namespace SqlServerMCP;

public sealed class ToolRateLimiter
{
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly Dictionary<string, WindowState> _windows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _sync = new();

    public ToolRateLimiter(int maxRequests, TimeSpan window)
    {
        _maxRequests = maxRequests > 0 ? maxRequests : 30;
        _window = window > TimeSpan.Zero ? window : TimeSpan.FromSeconds(60);
    }

    public bool TryAcquire(string key, out int retryAfterSeconds)
    {
        var now = DateTimeOffset.UtcNow;

        lock (_sync)
        {
            if (!_windows.TryGetValue(key, out var state) || state.WindowStart + _window <= now)
            {
                _windows[key] = new WindowState(now, 1);
                retryAfterSeconds = 0;
                return true;
            }

            if (state.Count >= _maxRequests)
            {
                var remaining = (state.WindowStart + _window) - now;
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
                return false;
            }

            state.Count++;
            _windows[key] = state;
            retryAfterSeconds = 0;
            return true;
        }
    }

    private sealed record WindowState(DateTimeOffset WindowStart, int Count)
    {
        public int Count { get; set; } = Count;
    }
}