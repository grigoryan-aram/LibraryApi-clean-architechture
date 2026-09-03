using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using LibraryApi.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Categorys.Commands
{
    public class AddCategoryCommandHandler : IRequestHandler<AddCategoryCommand, ErrorOr<CategorysDTO>>
    {

        private readonly ICategorysRepository _categorysRepository;
        private readonly ILogger<AddCategoryCommandHandler> _logger;

        public AddCategoryCommandHandler(
            ICategorysRepository categorysRepository,
            ILogger<AddCategoryCommandHandler> logger)
        {
            _categorysRepository = categorysRepository;
            _logger = logger;
        }

        public async Task<ErrorOr<CategorysDTO>> Handle(AddCategoryCommand request, CancellationToken cancellationToken)
        {
            var category = request.Adapt<CategoryModel>();

            var result = await _categorysRepository.AddCategoryAsync(category, cancellationToken);

            if (result is null)
            {
                _logger.LogError(
                    "The categorys repository returned no row when adding {Title}.",
                    request.title);

                return Error.Failure("Category.Add", "Failed to add category");
            }

            _logger.LogInformation(
                "Added category {CategoryId} ({Name}).",
                result.Id,
                result.Name);

            return result.Adapt<CategorysDTO>();
        }
    }
}
