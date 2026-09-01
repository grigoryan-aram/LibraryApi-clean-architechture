using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using Mapster;
using MediatR;

namespace Application.Features.Loans.Queries
{
    public class GetMyLoansQueryHandler
        : IRequestHandler<GetMyLoansQuery, ErrorOr<IReadOnlyList<LoansDTO>>>
    {
        private readonly ILoansRepository _loansRepository;
        private readonly IMembersRepository _membersRepository;

        public GetMyLoansQueryHandler(
            ILoansRepository loansRepository,
            IMembersRepository membersRepository)
        {
            _loansRepository = loansRepository;
            _membersRepository = membersRepository;
        }

        public async Task<ErrorOr<IReadOnlyList<LoansDTO>>> Handle(
            GetMyLoansQuery request,
            CancellationToken cancellationToken)
        {
            var member = await _membersRepository.GetMemberByIdentityUserIdAsync(
                request.IdentityUserId,
                cancellationToken);

            if (member is null)
            {
                return Error.NotFound(
                    "Loans.NoMemberForAccount",
                    "This account is not linked to a library member, so it " +
                    "cannot borrow and has no loans.");
            }

            var loans = await _loansRepository.GetLoansForMemberAsync(
                member.Id,
                cancellationToken);

            return ErrorOrFactory.From(loans.Adapt<IReadOnlyList<LoansDTO>>());
        }
    }
}
