using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Categorys.Commands
{
    public class UpdateCategoryCommandHandler
        : IRequestHandler<UpdateCategoryCommand, ErrorOr<CategorysDTO>>
    {
        private readonly ICategorysRepository _categorysRepository;
        private readonly ILogger<UpdateCategoryCommandHandler> _logger;

        public UpdateCategoryCommandHandler(
            ICategorysRepository categorysRepository,
            ILogger<UpdateCategoryCommandHandler> logger)
        {
            _categorysRepository = categorysRepository;
            _logger = logger;
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
                _logger.LogWarning(
                    "Rejected updating category {CategoryId}: no such category.",
                    request.Id);

                return Error.NotFound(
                    "Categorys.NotFound",
                    $"No category with id {request.Id}.");
            }

            category.Name = request.Name;

            var updated = await _categorysRepository.UpdateCategoryAsync(
                category,
                cancellationToken);

            _logger.LogInformation(
                "Updated category {CategoryId} ({Name}).",
                updated.Id,
                updated.Name);

            return updated.Adapt<CategorysDTO>();
        }
    }
}
