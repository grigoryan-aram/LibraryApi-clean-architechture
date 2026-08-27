using Application.DTOs;
using ErrorOr;
using MediatR;
namespace Application.Features.Loans.Commands
{
    // BorrowedAt, DueAt and ReturnedAt are deliberately absent. The clock is
    // the server's: a caller who can pick their own borrow date can pick their
    // own due date with it, and a loan that arrives already returned is not a
    // loan. Returning is POST /api/Loans/{id}/return.
    public record AddLoanCommand(
        int BookId,
        int MemberId) :
        IRequest<ErrorOr<LoansDTO>>;


}
