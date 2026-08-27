using Application.DTOs;
using Application.RepositoryInterfaces;
using ErrorOr;
using Mapster;
using MediatR;

namespace Application.Features.Loans.Commands
{
    public class ReturnLoanCommandHandler
        : IRequestHandler<ReturnLoanCommand, ErrorOr<LoansDTO>>
    {
        private readonly ILoansRepository _loansRepository;

        public ReturnLoanCommandHandler(ILoansRepository loansRepository)
        {
            _loansRepository = loansRepository;
        }

        public async Task<ErrorOr<LoansDTO>> Handle(
            ReturnLoanCommand request,
            CancellationToken cancellationToken)
        {
            var loan = await _loansRepository.GetLoanByIdAsync(request.Id, cancellationToken);

            if (loan is null)
            {
                return Error.NotFound(
                    "Loans.NotFound",
                    $"No loan with id {request.Id}.");
            }

            // Returning a book twice would move ReturnedAt forward and quietly
            // rewrite when the book actually came back. Refuse instead, and
            // say when it was returned so the caller can see why.
            if (loan.ReturnedAt != null)
            {
                return Error.Conflict(
                    "Loans.AlreadyReturned",
                    $"Loan {loan.Id} was already returned on " +
                    $"{loan.ReturnedAt:yyyy-MM-dd HH:mm} UTC.");
            }

            loan.ReturnedAt = DateTime.UtcNow;

            var updated = await _loansRepository.UpdateLoanAsync(loan, cancellationToken);

            return updated.Adapt<LoansDTO>();
        }
    }
}
