using ErrorOr;
using LibraryApi.Application.DTOs;
using MediatR;
namespace Application.Features.Loans.Commands
{
    public record AddLoanCommand(
        int BookId,
        int MemberId,
        DateTime BorrowedAt,
        DateTime? ReturnedAt) :
        IRequest<ErrorOr<LoansDTO>>;


}
