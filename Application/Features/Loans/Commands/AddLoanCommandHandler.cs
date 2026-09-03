using Application.DTOs;
using Application.RepositoryInterfaces;
using Application.ServiceInterfaces;
using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;
namespace Application.Features.Loans.Commands
{
    public class AddLoanCommandHandler : IRequestHandler<AddLoanCommand, ErrorOr<LoansDTO>>
    {

        private readonly ILoansRepository _loansRepository;
        private readonly IBooksRepository _booksRepository;
        private readonly IMembersRepository _membersRepository;
        private readonly ILoanPolicy _loanPolicy;
        private readonly ILogger<AddLoanCommandHandler> _logger;

        public AddLoanCommandHandler(
            ILoansRepository loansRepository,
            IBooksRepository booksRepository,
            IMembersRepository membersRepository,
            ILoanPolicy loanPolicy,
            ILogger<AddLoanCommandHandler> logger)
        {
            _loansRepository = loansRepository;
            _booksRepository = booksRepository;
            _membersRepository = membersRepository;
            _loanPolicy = loanPolicy;
            _logger = logger;
        }



        public async Task<ErrorOr<LoansDTO>> Handle(AddLoanCommand request, CancellationToken cancellationToken)
        {
            // Both ids are foreign keys. Without these two checks an unknown
            // id reaches SQL Server and comes back as a constraint violation,
            // which the exception middleware turns into a 500 — an
            // unrecoverable server error for what is plainly a bad request.
            var book = await _booksRepository.GetByIdAsync(request.BookId, cancellationToken);

            if (book is null)
            {
                _logger.LogWarning(
                    "Rejected loan: no book with id {BookId}.",
                    request.BookId);

                return Error.NotFound(
                    "Loans.BookNotFound",
                    $"No book with id {request.BookId}.");
            }

            var member = await _membersRepository.GetMemberByIdAsync(request.MemberId, cancellationToken);

            if (member is null)
            {
                _logger.LogWarning(
                    "Rejected loan of book {BookId}: no member with id {MemberId}.",
                    request.BookId,
                    request.MemberId);

                return Error.NotFound(
                    "Loans.MemberNotFound",
                    $"No member with id {request.MemberId}.");
            }

            var copiesOnLoan = await _loansRepository.CountActiveLoansForBookAsync(
                request.BookId,
                cancellationToken);

            if (copiesOnLoan >= book.TotalCopies)
            {
                _logger.LogWarning(
                    "Rejected loan of book {BookId} to member {MemberId}: all " +
                    "{TotalCopies} copies are out.",
                    request.BookId,
                    request.MemberId,
                    book.TotalCopies);

                return Error.Conflict(
                    "Loans.NoCopiesAvailable",
                    book.TotalCopies == 1
                        ? $"The only copy of \"{book.Title}\" is on loan."
                        : $"All {book.TotalCopies} copies of \"{book.Title}\" are on loan.");
            }

            var borrowedAt = DateTime.UtcNow;

            var loan = new LoanModel
            {
                BookId = request.BookId,
                MemberId = request.MemberId,
                BorrowedAt = borrowedAt,
                DueAt = _loanPolicy.DueDateFor(borrowedAt),
                ReturnedAt = null
            };

            var result = await _loansRepository.AddLoanAsync(loan, cancellationToken);

            if (result == null)
            {
                _logger.LogError(
                    "The loans repository returned no row when lending book " +
                    "{BookId} to member {MemberId}.",
                    request.BookId,
                    request.MemberId);

                return Error.Failure("Loans.NotCreated", "Could not add the loan.");
            }

            _logger.LogInformation(
                "Lent book {BookId} to member {MemberId} as loan {LoanId}, due {DueAt:u}.",
                result.BookId,
                result.MemberId,
                result.Id,
                result.DueAt);

            return result.Adapt<LoansDTO>();
        }
    }
}
