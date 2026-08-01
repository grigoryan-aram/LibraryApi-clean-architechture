using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using LibraryApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Infrastructure.Repositories
{
    public class LoansRepository : ILoansRepository
    {
        private readonly LibraryDBContext _context;

        public LoansRepository(LibraryDBContext context)
        {
            _context = context;
        }

        public async Task<LoanModel> AddLoanAsync(int bookId, int memberId, CancellationToken cancellationToken)
        {
            var loan = new LoanModel
            {
                BookId = bookId,
                MemberId = memberId
            };

            await _context.Loans.AddAsync(loan);
            await _context.SaveChangesAsync();
            return loan;
        }

        public async Task DeleteLoanAsync(int Id, CancellationToken cancellationtoken)
        {
            var loan = await _context.Loans.FindAsync(
               new object[] { Id },
               cancellationtoken
               );

            _context.Loans.Remove(loan!);

            await _context.SaveChangesAsync(cancellationtoken);
        }

        public async Task<IReadOnlyList<LoanModel>> GetAllLoansAsync(CancellationToken cancellationToken)
        {
            return await _context.Loans
                .AsNoTracking()
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
