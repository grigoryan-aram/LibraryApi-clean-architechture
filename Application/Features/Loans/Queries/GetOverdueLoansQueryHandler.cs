using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Loans.Queries
{
    public class GetOverdueLoansQueryHandler
        : IRequestHandler<GetOverdueLoansQuery, ErrorOr<IReadOnlyList<LoansDTO>>>
    {
        private readonly ILoansRepository _loansRepository;
        private readonly ILogger<GetOverdueLoansQueryHandler> _logger;

        public GetOverdueLoansQueryHandler(
            ILoansRepository loansRepository,
            ILogger<GetOverdueLoansQueryHandler> logger)
        {
            _loansRepository = loansRepository;
            _logger = logger;
        }

        public async Task<ErrorOr<IReadOnlyList<LoansDTO>>> Handle(
            GetOverdueLoansQuery request,
            CancellationToken cancellationToken)
        {
            var loans = await _loansRepository.GetOverdueLoansAsync(
                DateTime.UtcNow,
                cancellationToken);

            var loansDTO = loans.Adapt<IReadOnlyList<LoansDTO>>();

            _logger.LogInformation(
                "Returned {LoanCount} overdue loans.",
                loansDTO.Count);

            // No overdue loans is a perfectly good answer, so this returns an
            // empty list rather than NotFound.
            return ErrorOrFactory.From(loansDTO);
        }
    }
}
