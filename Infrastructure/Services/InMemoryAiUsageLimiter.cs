using Application.ServiceInterfaces;
using Infrastructure.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    // Process-local, like the chat history store. That means the allowance
    // resets when the app restarts, and a second instance behind a load
    // balancer keeps its own count — fine for one small deployment, but if
    // this ever needs to be authoritative it has to move to the database or
    // a distributed cache.
    public class InMemoryAiUsageLimiter : IAiUsageLimiter
    {
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _window;
        private readonly int _messagesPerWindow;
        private readonly ILogger<InMemoryAiUsageLimiter> _logger;

        private sealed record Usage(DateTimeOffset WindowStart, int Count);

        public InMemoryAiUsageLimiter(
            IMemoryCache cache,
            IOptions<ClaudeSettings> settings,
            ILogger<InMemoryAiUsageLimiter> logger)
        {
            _cache = cache;
            _window = TimeSpan.FromHours(settings.Value.RateLimitHours);
            _messagesPerWindow = settings.Value.MessagesPerWindow;
            _logger = logger;
        }

        public AiUsageDecision Check(string requester)
        {
            if (_messagesPerWindow <= 0)
            {
                return new AiUsageDecision(false, TimeSpan.Zero, Disabled: true);
            }

            if (_window <= TimeSpan.Zero)
            {
                return new AiUsageDecision(true, TimeSpan.Zero);
            }

            if (_cache.TryGetValue(Key(requester), out Usage? usage)
                && usage is not null
                && usage.Count >= _messagesPerWindow)
            {
                var retryAfter = usage.WindowStart + _window - DateTimeOffset.UtcNow;

                if (retryAfter > TimeSpan.Zero)
                {
                    return new AiUsageDecision(false, retryAfter);
                }
            }

            return new AiUsageDecision(true, TimeSpan.Zero);
        }

        public void Record(string requester)
        {
            if (_window <= TimeSpan.Zero || _messagesPerWindow <= 0)
            {
                _logger.LogDebug(
                    "AI rate limiting is off (RateLimitHours is {RateLimitHours}), so " +
                    "nothing was recorded for this request.",
                    _window.TotalHours);

                return;
            }

            var now = DateTimeOffset.UtcNow;

            var usage = _cache.TryGetValue(Key(requester), out Usage? current)
                        && current is not null
                        && now < current.WindowStart + _window
                ? current with { Count = current.Count + 1 }
                : new Usage(now, 1);

            // Absolute, not sliding: the entry has to die exactly one window
            // after the first message, not one window after it was last looked at.
            _cache.Set(Key(requester), usage, usage.WindowStart + _window);
        }

        private static string Key(string requester) =>
            $"claude-allowance:{requester}";
    }
}
