using Application.ServiceInterfaces;
using ErrorOr;
using LibraryApi.Infrastructure.Data;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging
{
    /// <summary>
    /// Writes the event to the transactional outbox, never to the broker.
    /// </summary>
    /// <remarks>
    /// With the bus outbox configured, the scoped <see cref="IPublishEndpoint"/>
    /// adds an <c>OutboxMessage</c> row to this request's DbContext instead of
    /// talking to RabbitMQ; <c>SaveChangesAsync</c> persists it, inside the
    /// caller's transaction when there is one. MassTransit's delivery service
    /// then publishes it in the background. A request therefore never waits
    /// on the broker — the 5-second publish timeout the direct publisher needed
    /// is gone with it — and an event can no longer be lost between a commit
    /// and a publish.
    /// </remarks>
    public class OutboxEventPublisher : IEventPublisher
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly LibraryDBContext _context;
        private readonly ILogger<OutboxEventPublisher> _logger;

        public OutboxEventPublisher(
            IPublishEndpoint publishEndpoint,
            LibraryDBContext context,
            ILogger<OutboxEventPublisher> logger)
        {
            _publishEndpoint = publishEndpoint;
            _context = context;
            _logger = logger;
        }

        public async Task<ErrorOr<Success>> PublishAsync<TEvent>(
            TEvent integrationEvent,
            CancellationToken cancellationToken)
            where TEvent : class
        {
            try
            {
                await _publishEndpoint.Publish(integrationEvent, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Recorded {EventType} in the outbox.",
                    typeof(TEvent).Name);

                return Result.Success;
            }
            catch (Exception exception)
            {
                // Mapped at the boundary, like ClaudeService. Unexpected, not
                // Failure: the caller rolls its write back and nothing they
                // sent was wrong.
                _logger.LogError(
                    exception,
                    "Could not record {EventType} in the outbox.",
                    typeof(TEvent).Name);

                return Error.Unexpected(
                    "Events.NotRecorded",
                    $"Could not record {typeof(TEvent).Name}.");
            }
        }
    }
}
