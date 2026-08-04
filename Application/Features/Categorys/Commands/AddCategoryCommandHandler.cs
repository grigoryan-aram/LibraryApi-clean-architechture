using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using LibraryApi.Domain.Entities;
using Mapster;
using MediatR;

namespace Application.Features.Categorys.Commands
{
    public class AddCategoryCommandHandler : IRequestHandler<AddCategoryCommand, ErrorOr<CategorysDTO>>
    {

        private readonly ICategorysRepository _categorysRepository;

        public AddCategoryCommandHandler(ICategorysRepository categorysRepository)
        {
            _categorysRepository = categorysRepository;
        }

        public async Task<ErrorOr<CategorysDTO>> Handle(AddCategoryCommand request, CancellationToken cancellationToken)
        {
            var category = request.Adapt<CategoryModel>();

            var result = await _categorysRepository.AddCategoryAsync(category, cancellationToken);

            if (result is null)
            {
                return Error.Failure("Category.Add", "Failed to add category");
            }

            return result.Adapt<CategorysDTO>();
        }
    }
}
