using ErrorOr;
using LibraryApi.Application.DTOs;
using MediatR;

namespace Application.Features.Categorys.Commands
{
    public record AddCategoryCommand(
           int id,
           string title) :
           IRequest<ErrorOr<CategorysDTO>>;

}
