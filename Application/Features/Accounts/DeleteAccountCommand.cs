using ErrorOr;
using MediatR;

namespace Application.Features.Accounts
{
    // IdentityUserId is never model-bound: the controller and the account page
    // both take it from the signed-in user's claims, or anyone could delete
    // anyone by putting an id in the body.
    public record DeleteAccountCommand(
        string IdentityUserId,
        string Password)
        : IRequest<ErrorOr<Deleted>>;
}
