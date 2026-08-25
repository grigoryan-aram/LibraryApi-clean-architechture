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

        Task DeleteLoanAsync(int Id, CancellationToken cancellationToken);
        Task<LoanModel?> GetLoanByIdAsync(int id, CancellationToken cancellationToken);

    }
}
