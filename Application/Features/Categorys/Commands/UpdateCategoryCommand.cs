using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Categorys.Commands
{
    public record UpdateCategoryCommand(
        int Id,
        string Name) :
        IRequest<ErrorOr<CategorysDTO>>;

}
