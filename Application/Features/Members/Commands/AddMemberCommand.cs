using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Members.Commands
{
    public record AddMemberCommand(
        int id,
        string name) : IRequest<ErrorOr<MembersDTO>>;


}
