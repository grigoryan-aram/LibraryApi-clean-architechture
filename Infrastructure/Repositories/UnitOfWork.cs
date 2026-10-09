using Application.ServiceInterfaces;
using ErrorOr;
using LibraryApi.Infrastructure.Data;

namespace Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly LibraryDBContext _context;

        public UnitOfWork(LibraryDBContext context)
        {
            _context = context;
        }

        public async Task<ErrorOr<T>> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<ErrorOr<T>>> work,
            CancellationToken cancellationToken)
        {
            // Already inside one: join it, and let the outer caller decide.
            if (_context.Database.CurrentTransaction is not null)
            {
                return await work(cancellationToken);
            }

            // Disposing without a commit rolls back, which covers a throw.
            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            var result = await work(cancellationToken);

            if (result.IsError)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return result;
            }

            // Not cancellable: by now the work is done, and a commit abandoned
            // halfway leaves the caller unable to tell what happened.
            await transaction.CommitAsync(CancellationToken.None);

            return result;
        }
    }
}
