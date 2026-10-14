using ErrorOr;
using MediatR;

namespace Application.Features.Accounts
{
    // IdentityUserId is never model-bound — see DeleteAccountCommand.
    public record ChangePasswordCommand(
        string IdentityUserId,
        string CurrentPassword,
        string NewPassword)
        : IRequest<ErrorOr<Success>>;
}
