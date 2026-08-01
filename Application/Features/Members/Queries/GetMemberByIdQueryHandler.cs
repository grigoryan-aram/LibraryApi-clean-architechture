using ErrorOr;
using LibraryApi.Application.DTOs;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Mapster;
using MediatR;

namespace Application.Features.Members.Queries
{
    internal class GetMemberByIdQueryHandler : IRequestHandler<GetMemberByIdQuery, ErrorOr<MembersDTO>>
    {
        private readonly IMembersRepository _membersRepository;


        public GetMemberByIdQueryHandler(IMembersRepository membersRepository)
        {
            _membersRepository = membersRepository;
        }

        public async Task<ErrorOr<MembersDTO>> Handle(GetMemberByIdQuery request, CancellationToken cancellationToken)
        {
            var member = request.Adapt<MemberModel>();

            var result = await _membersRepository.GetMemberByIdAsync(member.Id, cancellationToken);


            if (result == null)
            {
                return Error.NotFound("Member not found");

            }

            return result.Adapt<MembersDTO>();
        }
    }
}
