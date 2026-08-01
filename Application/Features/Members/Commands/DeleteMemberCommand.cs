using ErrorOr;
using MediatR;

namespace Application.Features.Members.Commands
{
    public record DeleteMemberCommand(int Id) : IRequest<ErrorOr<Deleted>>;


}
