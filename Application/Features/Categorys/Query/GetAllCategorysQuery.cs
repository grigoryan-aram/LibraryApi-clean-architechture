using Application.DTOs;
using ErrorOr;
using MediatR;
namespace Application.Features.Categorys.Query
{
    public record GetAllCategorysQuery :
    IRequest<ErrorOr<IReadOnlyList<CategorysDTO>>>;
}
