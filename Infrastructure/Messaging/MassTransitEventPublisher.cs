using Application.ServiceInterfaces;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging
{
    public class MassTransitEventPublisher : IEventPublisher
    {
        /// <summary>
        /// How long a publish may hold up the request that triggered it.
        /// </summary>
        /// <remarks>
        /// This exists because MassTransit's <c>Publish</c> does not fail fast
        /// when the broker is unreachable — it waits for the bus to connect,
        /// and the bus retries indefinitely. Measured: without this, lending a
        /// book against a configured-but-down RabbitMQ hung the HTTP request
        /// rather than returning, which is worse than the failure the
        /// try/catch below was written for.
        /// </remarks>
        private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);

        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<MassTransitEventPublisher> _logger;

        public MassTransitEventPublisher(
            IPublishEndpoint publishEndpoint,
            ILogger<MassTransitEventPublisher> logger)
        {
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task<ErrorOr<Success>> PublishAsync<TEvent>(
            TEvent integrationEvent,
            CancellationToken cancellationToken)
            where TEvent : class
        {
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bounded.CancelAfter(PublishTimeout);

            try
            {
                await _publishEndpoint.Publish(integrationEvent, bounded.Token);

                _logger.LogInformation("Published {EventType}.", typeof(TEvent).Name);

                return Result.Success;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Our own deadline, not the caller hanging up.
                _logger.LogError(
                    "Gave up publishing {EventType} after {Seconds}s — the broker did not answer.",
                    typeof(TEvent).Name,
                    PublishTimeout.TotalSeconds);

                return Error.Failure(
                    "Events.PublishTimedOut",
                    $"Publishing {typeof(TEvent).Name} timed out after " +
                    $"{PublishTimeout.TotalSeconds} seconds.");
            }
            catch (Exception exception)
            { 
                // Mapped rather than rethrown, at the boundary where the
                // third-party library throws — the same treatment ClaudeService
                // gives the Anthropic SDK. A broker outage is not the caller's
                // problem and must not become their 500.
                _logger.LogError(
                    exception,
                    "Could not publish {EventType}.",
                    typeof(TEvent).Name);

                return Error.Failure(
                    "Events.PublishFailed",
                    $"Could not publish {typeof(TEvent).Name}: {exception.Message}");
            }
        }
    }
}
