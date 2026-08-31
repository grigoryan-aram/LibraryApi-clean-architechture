using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Members.Commands
{
    public record UpdateMemberCommand(
        int Id,
        string Name) : IRequest<ErrorOr<MembersDTO>>;

}
