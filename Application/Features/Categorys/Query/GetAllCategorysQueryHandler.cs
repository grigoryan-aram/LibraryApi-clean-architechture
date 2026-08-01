using ErrorOr;
using LibraryApi.Application.DTOs;
using LibraryApi.Application.RepositoryInterfaces;
using Mapster;
using MediatR;
namespace Application.Features.Categorys.Query
{
    public class GetAllCategorysQueryHandler : IRequestHandler<GetAllCategorysQuery, ErrorOr<IReadOnlyList<CategorysDTO>>>
    {

        private readonly ICategorysRepository _categorysRepository;

        public GetAllCategorysQueryHandler(ICategorysRepository categorysRepository)
        {
            _categorysRepository = categorysRepository;
        }


        public async Task<ErrorOr<IReadOnlyList<CategorysDTO>>> Handle(GetAllCategorysQuery request, CancellationToken cancellationToken)
        {

            var categories = await _categorysRepository.GetAllCategoriesAsync(cancellationToken);

            if (categories == null)
            {

                return Error.NotFound("Categorys.NotFound", "No categories found.");

            }
            var categoriesDTO = categories.Adapt<IReadOnlyList<CategorysDTO>>();

            return ErrorOrFactory.From(categoriesDTO);


        }
    }
}
