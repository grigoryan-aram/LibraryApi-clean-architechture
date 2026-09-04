using Application.DTOs;
using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Members.Commands
{
    public class AddMemberCommandHandler : IRequestHandler<AddMemberCommand, ErrorOr<MembersDTO>>
    {

        private readonly IMembersRepository _membersRepository;
        private readonly ILogger<AddMemberCommandHandler> _logger;

        public AddMemberCommandHandler(
            IMembersRepository membersRepository,
            ILogger<AddMemberCommandHandler> logger)
        {
            _membersRepository = membersRepository;
            _logger = logger;
        }


        public async Task<ErrorOr<MembersDTO>> Handle(AddMemberCommand request, CancellationToken cancellationToken)
        {
            var member = new MemberModel
            {
                Name = request.Name
            };

            var result = await _membersRepository.AddMemberAsync(member, cancellationToken);

            if (result == null)
            {
                _logger.LogError(
                    "The members repository returned no row when adding {Name}.",
                    request.Name);

                _logger.LogWarning("no rows returned in memebers");

                return Error.Failure("failed to add member", "a failure has occurred");
            }

            _logger.LogInformation(
                "Added member {MemberId} ({Name}).",
                result.Id,
                result.Name);

            return result.Adapt<MembersDTO>();
        }

    }
}
