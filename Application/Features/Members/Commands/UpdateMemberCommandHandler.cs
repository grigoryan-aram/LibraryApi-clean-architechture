using Application.DTOs;
using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using Mapster;
using MediatR;

namespace Application.Features.Members.Commands
{
    public class UpdateMemberCommandHandler
        : IRequestHandler<UpdateMemberCommand, ErrorOr<MembersDTO>>
    {
        private readonly IMembersRepository _membersRepository;

        public UpdateMemberCommandHandler(IMembersRepository membersRepository)
        {
            _membersRepository = membersRepository;
        }

        public async Task<ErrorOr<MembersDTO>> Handle(
            UpdateMemberCommand request,
            CancellationToken cancellationToken)
        {
            var member = await _membersRepository.GetMemberByIdAsync(
                request.Id,
                cancellationToken);

            if (member is null)
            {
                return Error.NotFound(
                    "Members.NotFound",
                    $"No member with id {request.Id}.");
            }

            member.Name = request.Name;

            var updated = await _membersRepository.UpdateMemberAsync(
                member,
                cancellationToken);

            return updated.Adapt<MembersDTO>();
        }
    }
}
