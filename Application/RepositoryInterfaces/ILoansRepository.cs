using LibraryApi.Domain.Entities;

namespace Application.RepositoryInterfaces
{
    public interface ILoansRepository
    {
        Task<IReadOnlyList<LoanModel>> GetAllLoansAsync(CancellationToken cancellationToken);

        // Takes the whole loan rather than two ids: the previous signature
        // dropped BorrowedAt on the floor, so every row was stored with
        // DateTime.MinValue. This also matches the other repositories, which
        // all take the entity.
        Task<LoanModel> AddLoanAsync(LoanModel loan, CancellationToken cancellationToken);

        // Writes back a loan that was read with GetLoanByIdAsync — which
        // returns it untracked, so the implementation has to attach it.
        Task<LoanModel> UpdateLoanAsync(LoanModel loan, CancellationToken cancellationToken);

        Task DeleteLoanAsync(int Id, CancellationToken cancellationToken);
        Task<LoanModel?> GetLoanByIdAsync(int id, CancellationToken cancellationToken);

        // Open loans past their due date. Takes the cutoff instead of reading
        // the clock itself, so the query is deterministic and testable.
        Task<IReadOnlyList<LoanModel>> GetOverdueLoansAsync(
            DateTime asOf,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<LoanModel>> GetLoansForMemberAsync(
            int memberId,
            CancellationToken cancellationToken);

        Task<int> CountActiveLoansForBookAsync(
            int bookId,
            CancellationToken cancellationToken);

        Task<IReadOnlyDictionary<int, int>> CountActiveLoansByBookAsync(
            CancellationToken cancellationToken);

    }
}
