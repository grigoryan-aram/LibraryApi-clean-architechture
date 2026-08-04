using Application.DTOs;
using ErrorOr;
using MediatR;
namespace Application.Features.Categorys.Query
{
    public record GetCategoryByIdQuery(
           int Id)
           : IRequest<ErrorOr<CategorysDTO>>;
}
