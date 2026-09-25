using LibraryApi.Domain.Entities;


namespace LibraryApi.Domain.RepositoryInterfaces
{
    public interface IBooksRepository
    {
        Task<BookModel> AddAsync(BookModel book, CancellationToken cancellationToken);
        Task<BookModel> UpdateAsync(BookModel book, CancellationToken cancellationToken);
        Task DeleteAsync(int id, CancellationToken cancellationToken);
        Task<BookModel?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<IReadOnlyList<BookModel>> GetAllAsync(CancellationToken cancellationToken);

        // Filtering, ordering and paging all happen in SQL. Returns the total
        // matching count alongside the page, because the caller cannot work it
        // out from a page it has already been truncated to.
        Task<(IReadOnlyList<BookModel> Items, int TotalCount)> SearchAsync(
            string? search,
            string? sortBy,
            bool descending,
            int skip,
            int take,
            CancellationToken cancellationToken);
    }
}
