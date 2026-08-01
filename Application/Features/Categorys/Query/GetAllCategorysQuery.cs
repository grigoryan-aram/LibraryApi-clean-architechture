using ErrorOr;
using LibraryApi.Application.DTOs;
using MediatR;
namespace Application.Features.Categorys.Query
{
    public record GetAllCategorysQuery :
    IRequest<ErrorOr<IReadOnlyList<CategorysDTO>>>;
}
