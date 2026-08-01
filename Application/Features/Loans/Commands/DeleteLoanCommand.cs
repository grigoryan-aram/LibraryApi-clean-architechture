using ErrorOr;
using MediatR;

namespace Application.Features.Loans.Commands
{
    public record DeleteLoanCommand(int Id) : IRequest<ErrorOr<Deleted>>;

}
