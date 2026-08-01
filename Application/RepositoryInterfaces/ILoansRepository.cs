using LibraryApi.Domain.Entities;

namespace LibraryApi.Application.RepositoryInterfaces
{
    public interface ILoansRepository
    {
        Task<IReadOnlyList<LoanModel>> GetAllLoansAsync(CancellationToken cancellationToken);
        Task<LoanModel> AddLoanAsync(int bookId, int memberId, CancellationToken cancellationToken);
        Task DeleteLoanAsync(int Id, CancellationToken cancellationToken);
        Task<LoanModel?> GetLoanByIdAsync(int id, CancellationToken cancellationToken);

    }
}
