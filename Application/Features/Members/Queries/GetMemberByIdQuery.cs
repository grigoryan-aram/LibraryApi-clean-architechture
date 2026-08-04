using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Members.Queries
{
    public record GetMemberByIdQuery(int Id)
        : IRequest<ErrorOr<MembersDTO>>;


}
