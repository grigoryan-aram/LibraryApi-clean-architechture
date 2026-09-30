using LibraryApi.Domain.Entities;
using LibraryApi.Domain.RepositoryInterfaces;
using LibraryApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class PasswordResetCodeRepository : IPasswordResetCodeRepository
    {
        private readonly LibraryDBContext _context;

        public PasswordResetCodeRepository(LibraryDBContext context)
        {
            _context = context;
        }

        public async Task<PasswordResetCodeModel> AddAsync(
            PasswordResetCodeModel code,
            CancellationToken cancellationToken)
        {
            await _context.PasswordResetCodes.AddAsync(code, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            return code;
        }

        public async Task<PasswordResetCodeModel?> GetActiveAsync(
            string identityUserId,
            DateTime asOf,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(identityUserId))
            {
                return null;
            }

            return await _context.PasswordResetCodes
                .AsNoTracking()
                .Where(c => c.IdentityUserId == identityUserId && c.ExpiresAt > asOf)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<PasswordResetCodeModel> UpdateAsync(
            PasswordResetCodeModel code,
            CancellationToken cancellationToken)
        {
            _context.PasswordResetCodes.Update(code);

            await _context.SaveChangesAsync(cancellationToken);

            return code;
        }

        public async Task DeleteAllForUserAsync(
            string identityUserId,
            CancellationToken cancellationToken)
        {
            await _context.PasswordResetCodes
                .Where(c => c.IdentityUserId == identityUserId)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
