using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;

namespace Application.Features.Loans.Queries
{
    public class GetOverdueLoansQueryHandler
        : IRequestHandler<GetOverdueLoansQuery, ErrorOr<IReadOnlyList<LoansDTO>>>
    {
        private readonly ILoansRepository _loansRepository;

        public GetOverdueLoansQueryHandler(ILoansRepository loansRepository)
        {
            _loansRepository = loansRepository;
        }

        public async Task<ErrorOr<IReadOnlyList<LoansDTO>>> Handle(
            GetOverdueLoansQuery request,
            CancellationToken cancellationToken)
        {
            var loans = await _loansRepository.GetOverdueLoansAsync(
                DateTime.UtcNow,
                cancellationToken);

            // No overdue loans is a perfectly good answer, so this returns an
            // empty list rather than NotFound.
            return ErrorOrFactory.From(loans.Adapt<IReadOnlyList<LoansDTO>>());
        }
    }
}
