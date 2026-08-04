using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Members.Queries
{
    public record GetAllMembersQuery
        : IRequest<ErrorOr<IReadOnlyList<MembersDTO>>>;


}
