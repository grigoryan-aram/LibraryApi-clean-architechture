using ErrorOr;
using MediatR;

namespace Application.Features.PasswordReset
{
    public record ForgotPasswordCommand(
        string Email) : IRequest<ErrorOr<Success>>;
}
