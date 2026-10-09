using Application.DTOs;
using Application.IntegrationEvents;
using LibraryApi.Domain.RepositoryInterfaces;
using Application.ServiceInterfaces;
using ErrorOr;
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
        private readonly IEventPublisher _events;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AddLoanCommandHandler> _logger;

        public AddLoanCommandHandler(
            ILoansRepository loansRepository,
            IBooksRepository booksRepository,
            IMembersRepository membersRepository,
            ILoanPolicy loanPolicy,
            IEventPublisher events,
            IUnitOfWork unitOfWork,
            ILogger<AddLoanCommandHandler> logger)
        {
            _loansRepository = loansRepository;
            _booksRepository = booksRepository;
            _membersRepository = membersRepository;
            _loanPolicy = loanPolicy;
            _events = events;
            _unitOfWork = unitOfWork;
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

            // The loan and its BookBorrowed event commit together or not at
            // all. The event needs the loan's generated id, so these are two
            // saves, and the transaction is what makes them one.
            var lent = await _unitOfWork.ExecuteInTransactionAsync<LoanModel>(async token =>
            {
                var saved = await _loansRepository.AddLoanAsync(loan, token);

                if (saved == null)
                {
                    _logger.LogError(
                        "The loans repository returned no row when lending book " +
                        "{BookId} to member {MemberId}.",
                        request.BookId,
                        request.MemberId);

                    return Error.Failure("Loans.NotCreated", "Could not add the loan.");
                }

                var published = await _events.PublishAsync(
                    new BookBorrowed(
                        saved.Id,
                        saved.BookId,
                        book.Title,
                        saved.MemberId,
                        saved.BorrowedAt,
                        saved.DueAt),
                    token);

                if (published.IsError)
                {
                    return published.Errors;
                }

                return saved;
            }, cancellationToken);

            if (lent.IsError)
            {
                return lent.Errors;
            }

            _logger.LogInformation(
                "Lent book {BookId} to member {MemberId} as loan {LoanId}, due {DueAt:u}.",
                lent.Value.BookId,
                lent.Value.MemberId,
                lent.Value.Id,
                lent.Value.DueAt);

            return lent.Value.Adapt<LoansDTO>();
        }
    }
}
