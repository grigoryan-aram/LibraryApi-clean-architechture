using ErrorOr;
using LibraryApi.Application.DTOs;
using MediatR;

namespace Application.Features.Members.Queries
{
    public record GetAllMembersQuery : IRequest<ErrorOr<IReadOnlyList<MembersDTO>>>;


}
