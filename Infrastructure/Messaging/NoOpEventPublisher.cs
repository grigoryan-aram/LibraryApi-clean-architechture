using Application.ServiceInterfaces;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging
{
    /// <summary>
    /// Used when no broker is configured. Reports success, because from the
    /// caller's point of view nothing failed — there is simply nowhere to
    /// publish. Logged at Debug so a misconfigured environment is findable
    /// without filling the log on one that never wanted a broker.
    /// </summary>
    public class NoOpEventPublisher : IEventPublisher
    {
        private readonly ILogger<NoOpEventPublisher> _logger;

        public NoOpEventPublisher(ILogger<NoOpEventPublisher> logger)
        {
            _logger = logger;
        }

        public Task<ErrorOr<Success>> PublishAsync<TEvent>(
            TEvent integrationEvent,
            CancellationToken cancellationToken)
            where TEvent : class
        {
            _logger.LogDebug(
                "No broker configured; dropped {EventType}.",
                typeof(TEvent).Name);

            return Task.FromResult<ErrorOr<Success>>(Result.Success);
        }
    }
}
