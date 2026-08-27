using Application.DTOs;
using Application.RepositoryInterfaces;
using Application.ServiceInterfaces;
using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Mapster;
using MediatR;
namespace Application.Features.Loans.Commands
{
    public class AddLoanCommandHandler : IRequestHandler<AddLoanCommand, ErrorOr<LoansDTO>>
    {

        private readonly ILoansRepository _loansRepository;
        private readonly IBooksRepository _booksRepository;
        private readonly IMembersRepository _membersRepository;
        private readonly ILoanPolicy _loanPolicy;

        public AddLoanCommandHandler(
            ILoansRepository loansRepository,
            IBooksRepository booksRepository,
            IMembersRepository membersRepository,
            ILoanPolicy loanPolicy)
        {
            _loansRepository = loansRepository;
            _booksRepository = booksRepository;
            _membersRepository = membersRepository;
            _loanPolicy = loanPolicy;
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
                return Error.NotFound(
                    "Loans.BookNotFound",
                    $"No book with id {request.BookId}.");
            }

            var member = await _membersRepository.GetMemberByIdAsync(request.MemberId, cancellationToken);

            if (member is null)
            {
                return Error.NotFound(
                    "Loans.MemberNotFound",
                    $"No member with id {request.MemberId}.");
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

                return Error.Failure("Loans.NotCreated", "Could not add the loan.");




            return result.Adapt<LoansDTO>();
        }
    }
}
