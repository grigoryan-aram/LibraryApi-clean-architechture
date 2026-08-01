using ErrorOr;
using LibraryApi.Application.DTOs;
using MediatR;
namespace Application.Features.Categorys.Query
{
    public record GetCategoryByIdQuery(
           int Id)
           : IRequest<ErrorOr<CategorysDTO>>;
}
