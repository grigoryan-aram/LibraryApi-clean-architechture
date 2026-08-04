using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;

namespace Application.Features.Categorys.Query
{
    public class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, ErrorOr<CategorysDTO>>
    {
        private readonly ICategorysRepository _categorysRepository;

        public GetCategoryByIdQueryHandler(ICategorysRepository categorysRepository)
        {
            _categorysRepository = categorysRepository;
        }


        public async Task<ErrorOr<CategorysDTO>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
        {

            var category = await _categorysRepository.GetCategoryByIdAsync(request.Id, cancellationToken);

            if (category == null)
            {
                return Error.NotFound("CategoryNotFound", $"Category with Id {request.Id} not found.");
            }

            var categoryDTO = category.Adapt<CategorysDTO>();

            return ErrorOrFactory.From(categoryDTO);

        }
    }
}
