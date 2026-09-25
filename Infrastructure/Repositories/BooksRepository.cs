using LibraryApi.Domain.RepositoryInterfaces;
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


        public async Task<BookModel> UpdateAsync(
            BookModel book,
            CancellationToken cancellationToken)
        {
            _context.Books.Update(book);

            await _context.SaveChangesAsync(cancellationToken);

            return book;
        }

        public async Task DeleteAsync(
            int id,
            CancellationToken cancellationToken)
        {
            await _context.Books
                .Where(b => b.Id == id)
                .ExecuteDeleteAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<BookModel>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return await _context.Books
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }


        public async Task<(IReadOnlyList<BookModel> Items, int TotalCount)> SearchAsync(
            string? search,
            string? sortBy,
            bool descending,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            var query = _context.Books.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();

                // EF translates this to LIKE %term%, so it runs in SQL Server
                // rather than pulling the table back to filter in memory. It
                // cannot use an index, which is fine at this size and is the
                // thing to revisit first if the catalogue grows.
                query = query.Where(b =>
                    b.Title.Contains(term) || b.Author.Contains(term));
            }

            // Counted before paging, so it reports how many rows match rather
            // than how many came back.
            var totalCount = await query.CountAsync(cancellationToken);

            // A switch rather than a dynamic property lookup: the sort key
            // arrives from the query string, and building an expression from
            // caller-supplied text is how an ordering parameter turns into an
            // injection surface. Unknown keys never reach here — the validator
            // rejects them — so the default is only for null or empty.
            query = (sortBy?.ToLowerInvariant(), descending) switch
            {
                ("title", false) => query.OrderBy(b => b.Title),
                ("title", true) => query.OrderByDescending(b => b.Title),
                ("author", false) => query.OrderBy(b => b.Author),
                ("author", true) => query.OrderByDescending(b => b.Author),
                ("totalcopies", false) => query.OrderBy(b => b.TotalCopies),
                ("totalcopies", true) => query.OrderByDescending(b => b.TotalCopies),
                (_, true) => query.OrderByDescending(b => b.Id),

                // Always ordered by something: Skip/Take on an unordered
                // query has no defined row order in SQL Server, so page 2
                // could legally repeat a row from page 1.
                _ => query.OrderBy(b => b.Id)
            };

            var items = await query
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
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
