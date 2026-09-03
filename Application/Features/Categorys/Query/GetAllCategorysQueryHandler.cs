using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;
namespace Application.Features.Categorys.Query
{
    public class GetAllCategorysQueryHandler : IRequestHandler<GetAllCategorysQuery, ErrorOr<IReadOnlyList<CategorysDTO>>>
    {

        private readonly ICategorysRepository _categorysRepository;
        private readonly ILogger<GetAllCategorysQueryHandler> _logger;

        public GetAllCategorysQueryHandler(
            ICategorysRepository categorysRepository,
            ILogger<GetAllCategorysQueryHandler> logger)
        {
            _categorysRepository = categorysRepository;
            _logger = logger;
        }


        public async Task<ErrorOr<IReadOnlyList<CategorysDTO>>> Handle(GetAllCategorysQuery request, CancellationToken cancellationToken)
        {

            var categories = await _categorysRepository.GetAllCategoriesAsync(cancellationToken);

            if (categories == null)
            {
                _logger.LogError("The categorys repository returned no collection.");

                return Error.NotFound("Categorys.NotFound", "No categories found.");

            }
            var categoriesDTO = categories.Adapt<IReadOnlyList<CategorysDTO>>();

            _logger.LogInformation(
                "Returned {CategoryCount} categories.",
                categoriesDTO.Count);

            return ErrorOrFactory.From(categoriesDTO);


        }
    }
}
