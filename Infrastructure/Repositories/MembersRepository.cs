using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using LibraryApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Infrastructure.Repositories
{
    public class MembersRepository : IMembersRepository
    {
        private readonly LibraryDBContext _context;

        public MembersRepository(LibraryDBContext context)
        {
            _context = context;
        }

        public async Task<MemberModel> AddMemberAsync(
            MemberModel member,
            CancellationToken cancellationToken)
        {
            await _context.Members.AddAsync(member, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            return member;
        }

        public async Task DeleteMemberAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var member = await _context.Members.FindAsync(
                new object[] { id },
                cancellationToken);

            if (member == null)
                return;

            _context.Members.Remove(member);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<MemberModel?> GetMemberByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return await _context.Members
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<MemberModel>> GetMembersAsync(
            CancellationToken cancellationToken)
        {
            return await _context.Members
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
    }
}
