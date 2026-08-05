using Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using LibraryApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
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
            await _context.Categories
                .Where(c => c.Id == id)
                .ExecuteDeleteAsync(cancellationToken);
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