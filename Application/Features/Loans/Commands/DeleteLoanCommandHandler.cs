using Application.RepositoryInterfaces;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
namespace Application.Features.Loans.Commands
{
    public class DeleteLoanCommandHandler : IRequestHandler<DeleteLoanCommand, ErrorOr<Deleted>>
    {

        private readonly ILoansRepository _loansRepository;
        private readonly ILogger<DeleteLoanCommandHandler> _logger;

        public DeleteLoanCommandHandler(
            ILoansRepository loansRepository,
            ILogger<DeleteLoanCommandHandler> logger)
        {
            _loansRepository = loansRepository;
            _logger = logger;
        }

        public async Task<ErrorOr<Deleted>> Handle(DeleteLoanCommand request, CancellationToken cancellationToken)
        {


            await _loansRepository.DeleteLoanAsync(request.Id, cancellationToken);

            _logger.LogInformation("Deleted loan {LoanId}.", request.Id);

            return Result.Deleted;
        }
    }
}
