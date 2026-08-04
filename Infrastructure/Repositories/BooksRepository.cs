using Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using LibraryApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{

    public class BooksRepository : IBooksRepository
    {
        private readonly LibraryDBContext _context;

        public BooksRepository(LibraryDBContext context)
        {
            _context = context;
        }

        public async Task<BookModel> AddAsync(
            BookModel book,
            CancellationToken cancellationToken)
        {
            await _context.Books.AddAsync(book, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            return book;
        }


        public async Task DeleteAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var book = await _context.Books.FindAsync(
                new object[] { id },
                cancellationToken);


            _context.Books.Remove(book!);

            await _context.SaveChangesAsync(cancellationToken);
        }


        public async Task<IReadOnlyList<BookModel>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return await _context.Books
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }


        public async Task<BookModel?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return await _context.Books
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }
    }
}
