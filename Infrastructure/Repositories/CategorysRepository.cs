using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;

using LibraryApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Infrastructure.Repositories
{
    public class CategorysRepository : ICategorysRepository
    {
        private readonly LibraryDBContext _context;

        public CategorysRepository(LibraryDBContext context)
        {
            _context = context;
        }


        public async Task<CategoryModel> AddCategoryAsync(
            CategoryModel category,
            CancellationToken cancellationToken)
        {
            await _context.Categories.AddAsync(
                category,
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            return category;
        }


        public async Task DeleteCategoryAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var category = await _context.Categories
                .FindAsync(new object[] { id }, cancellationToken);

            if (category == null)
                return;

            _context.Categories.Remove(category);

            await _context.SaveChangesAsync(cancellationToken);
        }


        public async Task<IReadOnlyList<CategoryModel>> GetAllCategoriesAsync(
            CancellationToken cancellationToken)
        {
            return await _context.Categories
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }


        public async Task<CategoryModel?> GetCategoryByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }


    }
}