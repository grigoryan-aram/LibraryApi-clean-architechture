using LibraryApi.Domain.Entities;


namespace LibraryApi.Application.RepositoryInterfaces
{
    public interface IBooksRepository
    {
        Task<BookModel> AddAsync(BookModel book, CancellationToken cancellationToken);
        Task DeleteAsync(int id, CancellationToken cancellationToken);
        Task<BookModel?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<IReadOnlyList<BookModel>> GetAllAsync(CancellationToken cancellationToken);
    }
}
