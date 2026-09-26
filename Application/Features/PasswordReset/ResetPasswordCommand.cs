using ErrorOr;
using MediatR;

namespace Application.Features.PasswordReset
{
    public record ResetPasswordCommand(
        string Email,
        string Code,
        string NewPassword) : IRequest<ErrorOr<Success>>;
}
