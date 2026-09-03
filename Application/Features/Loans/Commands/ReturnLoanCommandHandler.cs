using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Loans.Commands
{
    public class ReturnLoanCommandHandler
        : IRequestHandler<ReturnLoanCommand, ErrorOr<LoansDTO>>
    {
        private readonly ILoansRepository _loansRepository;
        private readonly ILogger<ReturnLoanCommandHandler> _logger;

        public ReturnLoanCommandHandler(
            ILoansRepository loansRepository,
            ILogger<ReturnLoanCommandHandler> logger)
        {
            _loansRepository = loansRepository;
            _logger = logger;
        }

        public async Task<ErrorOr<LoansDTO>> Handle(
            ReturnLoanCommand request,
            CancellationToken cancellationToken)
        {
            var loan = await _loansRepository.GetLoanByIdAsync(request.Id, cancellationToken);

            if (loan is null)
            {
                _logger.LogWarning(
                    "Rejected return: no loan with id {LoanId}.",
                    request.Id);

                return Error.NotFound(
                    "Loans.NotFound",
                    $"No loan with id {request.Id}.");
            }

            // Returning a book twice would move ReturnedAt forward and quietly
            // rewrite when the book actually came back. Refuse instead, and
            // say when it was returned so the caller can see why.
            if (loan.ReturnedAt != null)
            {
                _logger.LogWarning(
                    "Rejected return of loan {LoanId}: already returned at {ReturnedAt:u}.",
                    loan.Id,
                    loan.ReturnedAt);

                return Error.Conflict(
                    "Loans.AlreadyReturned",
                    $"Loan {loan.Id} was already returned on " +
                    $"{loan.ReturnedAt:yyyy-MM-dd HH:mm} UTC.");
            }

            loan.ReturnedAt = DateTime.UtcNow;

            var updated = await _loansRepository.UpdateLoanAsync(loan, cancellationToken);

            _logger.LogInformation(
                "Returned loan {LoanId} (book {BookId}, member {MemberId}) at {ReturnedAt:u}.",
                updated.Id,
                updated.BookId,
                updated.MemberId,
                updated.ReturnedAt);

            return updated.Adapt<LoansDTO>();
        }
    }
}
