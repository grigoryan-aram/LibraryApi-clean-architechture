using Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using LibraryApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class LoansRepository : ILoansRepository
    {
        private readonly LibraryDBContext _context;

        public LoansRepository(LibraryDBContext context)
        {
            _context = context;
        }

        public async Task<LoanModel> AddLoanAsync(LoanModel loan, CancellationToken cancellationToken)
        {
            await _context.Loans.AddAsync(loan, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return loan;
        }

        public async Task<LoanModel> UpdateLoanAsync(LoanModel loan, CancellationToken cancellationToken)
        {
            // Update() rather than plain SaveChanges: every read in this
            // repository is AsNoTracking, so the instance the handler mutated
            // is detached and the context knows nothing about it.
            _context.Loans.Update(loan);
            await _context.SaveChangesAsync(cancellationToken);
            return loan;
        }

        public async Task DeleteLoanAsync(
        int id,
        CancellationToken cancellationToken)
        {
            await _context.Loans
                .Where(l => l.Id == id)
                .ExecuteDeleteAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<LoanModel>> GetAllLoansAsync(CancellationToken cancellationToken)
        {
            return await _context.Loans
                .AsNoTracking()
                .ToListAsync(cancellationToken);


        }

        public async Task<IReadOnlyList<LoanModel>> GetOverdueLoansAsync(
            DateTime asOf,
            CancellationToken cancellationToken)
        {
            return await _context.Loans
                .AsNoTracking()
                .Where(l => l.ReturnedAt == null && l.DueAt < asOf)
                .OrderBy(l => l.DueAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<LoanModel?> GetLoanByIdAsync(int id, CancellationToken cancellationToken)
        {


            return await _context.Loans
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id,
                 cancellationToken);

        }
    }
}
