using Application.ServiceInterfaces;
using ErrorOr;

namespace Application.UnitTests.TestDoubles;

/// <summary>
/// Runs the work and records whether it would have committed.
/// </summary>
/// <remarks>
/// Mirrors the real UnitOfWork's rule — a value commits, an error rolls back —
/// so a handler test can assert that a write and its event were committed or
/// rolled back together without a database.
/// </remarks>
public sealed class RecordingUnitOfWork : IUnitOfWork
{
    public int Commits { get; private set; }

    public int Rollbacks { get; private set; }

    public async Task<ErrorOr<T>> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<ErrorOr<T>>> work,
        CancellationToken cancellationToken)
    {
        var result = await work(cancellationToken);

        if (result.IsError)
        {
            Rollbacks++;
        }
        else
        {
            Commits++;
        }

        return result;
    }
}
