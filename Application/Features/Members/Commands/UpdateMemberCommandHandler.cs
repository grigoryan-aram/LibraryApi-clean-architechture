using Application.DTOs;
using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Members.Commands
{
    public class UpdateMemberCommandHandler
        : IRequestHandler<UpdateMemberCommand, ErrorOr<MembersDTO>>
    {
        private readonly IMembersRepository _membersRepository;
        private readonly ILogger<UpdateMemberCommandHandler> _logger;

        public UpdateMemberCommandHandler(
            IMembersRepository membersRepository,
            ILogger<UpdateMemberCommandHandler> logger)
        {
            _membersRepository = membersRepository;
            _logger = logger;
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
                _logger.LogWarning(
                    "Rejected updating member {MemberId}: no such member.",
                    request.Id);

                return Error.NotFound(
                    "Members.NotFound",
                    $"No member with id {request.Id}.");
            }

            member.Name = request.Name;

            var updated = await _membersRepository.UpdateMemberAsync(
                member,
                cancellationToken);

            _logger.LogInformation(
                "Updated member {MemberId} ({Name}).",
                updated.Id,
                updated.Name);

            return updated.Adapt<MembersDTO>();
        }
    }
}
