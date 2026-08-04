using ErrorOr;
using MediatR;
namespace Application.Features.Categorys.Commands
{
    public record DeleteCategoryCommand(int Id)
        : IRequest<ErrorOr<Deleted>>;
}
