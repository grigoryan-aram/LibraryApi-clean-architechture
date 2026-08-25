using Application.ServiceInterfaces;
using Infrastructure.Settings;
using Microsoft.Extensions.Caching.Memory;
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

        public InMemoryAiUsageLimiter(
            IMemoryCache cache,
            IOptions<ClaudeSettings> settings)
        {
            _cache = cache;
            _window = TimeSpan.FromHours(settings.Value.RateLimitHours);
        }

        public AiUsageDecision Check(string requester)
        {
            if (_window <= TimeSpan.Zero)
            {
                return new AiUsageDecision(true, TimeSpan.Zero);
            }

            if (_cache.TryGetValue(Key(requester), out DateTimeOffset lastUsed))
            {
                var elapsed = DateTimeOffset.UtcNow - lastUsed;

                if (elapsed < _window)
                {
                    return new AiUsageDecision(false, _window - elapsed);
                }
            }

            return new AiUsageDecision(true, TimeSpan.Zero);
        }

        public void Record(string requester)
        {
            if (_window <= TimeSpan.Zero)
            {
                return;
            }

            // Absolute, not sliding: the entry has to die exactly one window
            // after the message, not one window after it was last looked at.
            _cache.Set(Key(requester), DateTimeOffset.UtcNow, _window);
        }

        private static string Key(string requester) =>
            $"claude-allowance:{requester}";
    }
}
