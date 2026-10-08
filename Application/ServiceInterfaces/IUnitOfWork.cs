using ErrorOr;

namespace Application.ServiceInterfaces
{
    /// <summary>
    /// Runs several writes as one database transaction.
    /// </summary>
    /// <remarks>
    /// Exists for the transactional outbox: an entity and the event announcing
    /// it are two <c>SaveChanges</c> calls (the event needs the id the first
    /// one generates), and they must commit or roll back together. Commits
    /// when <paramref name="work"/> returns a value, rolls back when it
    /// returns an error or throws.
    /// </remarks>
    public interface IUnitOfWork
    {
        Task<ErrorOr<T>> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<ErrorOr<T>>> work,
            CancellationToken cancellationToken);
    }
}
