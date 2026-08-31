using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;

namespace Application.Features.Categorys.Commands
{
    public class UpdateCategoryCommandHandler
        : IRequestHandler<UpdateCategoryCommand, ErrorOr<CategorysDTO>>
    {
        private readonly ICategorysRepository _categorysRepository;

        public UpdateCategoryCommandHandler(ICategorysRepository categorysRepository)
        {
            _categorysRepository = categorysRepository;
        }

        public async Task<ErrorOr<CategorysDTO>> Handle(
            UpdateCategoryCommand request,
            CancellationToken cancellationToken)
        {
            var category = await _categorysRepository.GetCategoryByIdAsync(
                request.Id,
                cancellationToken);

            if (category is null)
            {
                return Error.NotFound(
                    "Categorys.NotFound",
                    $"No category with id {request.Id}.");
            }

            category.Name = request.Name;

            var updated = await _categorysRepository.UpdateCategoryAsync(
                category,
                cancellationToken);

            return updated.Adapt<CategorysDTO>();
        }
    }
}
