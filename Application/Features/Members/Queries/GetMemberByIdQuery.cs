using ErrorOr;
using LibraryApi.Application.DTOs;
using MediatR;

namespace Application.Features.Members.Queries
{
    public record GetMemberByIdQuery(int Id) : IRequest<ErrorOr<MembersDTO>>;


}
