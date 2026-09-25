using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Categorys.Commands
{
    // Name, not "title": Mapster maps by member name, and CategoryModel calls
    // it Name — so a parameter called "title" silently mapped to nothing and
    // every category created through this command was saved with an empty
    // name. There is no client-supplied id either; the identity column
    // assigns it, exactly as AddMemberCommand was already fixed to do.
    public record AddCategoryCommand(
           string Name) :
           IRequest<ErrorOr<CategorysDTO>>;

}
