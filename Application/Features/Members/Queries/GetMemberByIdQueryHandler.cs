using Application.DTOs;
using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Members.Queries
{
    internal class GetMemberByIdQueryHandler : IRequestHandler<GetMemberByIdQuery, ErrorOr<MembersDTO>>
    {
        private readonly IMembersRepository _membersRepository;
        private readonly ILogger<GetMemberByIdQueryHandler> _logger;


        public GetMemberByIdQueryHandler(
            IMembersRepository membersRepository,
            ILogger<GetMemberByIdQueryHandler> logger)
        {
            _membersRepository = membersRepository;
            _logger = logger;
        }

        public async Task<ErrorOr<MembersDTO>> Handle(GetMemberByIdQuery request, CancellationToken cancellationToken)
        {
            var member = request.Adapt<MemberModel>();

            var result = await _membersRepository.GetMemberByIdAsync(member.Id, cancellationToken);


            if (result == null)
            {
                _logger.LogWarning("No member with id {MemberId}.", member.Id);

                return Error.NotFound("Member not found");

            }

            _logger.LogInformation(
                "Returned member {MemberId} ({Name}).",
                result.Id,
                result.Name);

            return result.Adapt<MembersDTO>();
        }
    }
}
