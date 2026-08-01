using ErrorOr;
using LibraryApi.Application.DTOs;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Mapster;
using MediatR;

namespace Application.Features.Members.Commands
{
    public class AddMemberCommandHandler : IRequestHandler<AddMemberCommand, ErrorOr<MembersDTO>>
    {

        private readonly IMembersRepository _membersRepository;
        public AddMemberCommandHandler(IMembersRepository membersRepository)
        {
            _membersRepository = membersRepository;
        }


        public async Task<ErrorOr<MembersDTO>> Handle(AddMemberCommand request, CancellationToken cancellationToken)
        {
            var member = request.Adapt<MemberModel>();

            var result = await _membersRepository.AddMemberAsync(member, cancellationToken);

            if (result == null)
            {

                return Error.Failure("failed to add member", "a failure has occurred");
            }

            return result.Adapt<MembersDTO>();
        }

    }
}
