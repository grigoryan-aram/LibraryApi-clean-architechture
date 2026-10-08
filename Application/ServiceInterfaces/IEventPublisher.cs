using ErrorOr;

namespace Application.ServiceInterfaces
{
    /// <summary>
    /// Records an integration event for delivery to whoever is listening.
    /// </summary>
    /// <remarks>
    /// An Application-owned abstraction so no MassTransit type reaches this
    /// layer — the same shape as <see cref="IEmailService"/>.
    ///
    /// With a broker configured this writes to the transactional outbox in
    /// the application database, never to the broker: delivery happens later,
    /// from a background service. Call it inside
    /// <see cref="IUnitOfWork.ExecuteInTransactionAsync{T}"/> together with
    /// the write it announces, and treat an error as a reason to roll that
    /// write back — the two must commit together or not at all.
    /// </remarks>
    public interface IEventPublisher
    {
        Task<ErrorOr<Success>> PublishAsync<TEvent>(
            TEvent integrationEvent,
            CancellationToken cancellationToken)
            where TEvent : class;
    }
}
