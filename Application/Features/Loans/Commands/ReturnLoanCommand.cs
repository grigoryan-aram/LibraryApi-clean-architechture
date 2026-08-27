using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Loans.Commands
{
    // Its own command rather than a general UpdateLoanCommand: returning a
    // book is the one change a loan record is meant to undergo, and it is a
    // single stamp of the server clock. An endpoint that could rewrite
    // BorrowedAt or DueAt would be an endpoint for corrupting the record.
    public record ReturnLoanCommand(int Id) : IRequest<ErrorOr<LoansDTO>>;

}
