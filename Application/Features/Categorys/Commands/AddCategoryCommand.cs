using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Categorys.Commands
{
    public record AddCategoryCommand(
           int id,
           string title) :
           IRequest<ErrorOr<CategorysDTO>>;

}
