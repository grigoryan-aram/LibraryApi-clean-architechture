using ErrorOr;

namespace Application.ServiceInterfaces
{
    /// <summary>
    /// Publishes an integration event to whoever is listening.
    /// </summary>
    /// <remarks>
    /// An Application-owned abstraction so no MassTransit type reaches this
    /// layer — the same shape as <see cref="IEmailService"/>.
    ///
    /// Returns <c>ErrorOr</c> rather than throwing, and callers treat a
    /// failure as a logged side effect rather than a failed request: the
    /// database write has already committed by the time anything is
    /// published, so a broker that is down must not turn a completed loan
    /// into an error the caller will retry.
    /// </remarks>
    public interface IEventPublisher
    {
        Task<ErrorOr<Success>> PublishAsync<TEvent>(
            TEvent integrationEvent,
            CancellationToken cancellationToken)
            where TEvent : class;
    }
}
