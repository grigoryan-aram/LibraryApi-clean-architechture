using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Categorys.Query
{
    public class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, ErrorOr<CategorysDTO>>
    {
        private readonly ICategorysRepository _categorysRepository;
        private readonly ILogger<GetCategoryByIdQueryHandler> _logger;

        public GetCategoryByIdQueryHandler(
            ICategorysRepository categorysRepository,
            ILogger<GetCategoryByIdQueryHandler> logger)
        {
            _categorysRepository = categorysRepository;
            _logger = logger;
        }


        public async Task<ErrorOr<CategorysDTO>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
        {

            var category = await _categorysRepository.GetCategoryByIdAsync(request.Id, cancellationToken);

            if (category == null)
            {
                _logger.LogWarning("No category with id {CategoryId}.", request.Id);

                return Error.NotFound("CategoryNotFound", $"Category with Id {request.Id} not found.");
            }

            var categoryDTO = category.Adapt<CategorysDTO>();

            _logger.LogInformation(
                "Returned category {CategoryId} ({Name}).",
                category.Id,
                category.Name);

            return ErrorOrFactory.From(categoryDTO);

        }
    }
}
