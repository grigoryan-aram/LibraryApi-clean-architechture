using LibraryApi.Domain.Entities;

namespace Application.RepositoryInterfaces
{
    public interface ICategorysRepository
    {
        Task<IReadOnlyList<CategoryModel>> GetAllCategoriesAsync(CancellationToken cancellationToken);
        Task<CategoryModel> AddCategoryAsync(CategoryModel category, CancellationToken cancellationToken);
        Task DeleteCategoryAsync(int id, CancellationToken cancellationToken);
        Task<CategoryModel?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken);

    }
}
