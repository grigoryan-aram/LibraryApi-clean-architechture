using Application.DTOs;
using Application.ServiceInterfaces;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Login.Commands
{
    public class LoginCommandHandler
       : IRequestHandler<LoginCommand, ErrorOr<LoginResponseDTO>>
    {
        private readonly IIdentityService _identityService;
        private readonly ILogger<LoginCommandHandler> _logger;

        public LoginCommandHandler(
            IIdentityService identityService,
            ILogger<LoginCommandHandler> logger)
        {
            _identityService = identityService;
            _logger = logger;
        }

        public async Task<ErrorOr<LoginResponseDTO>> Handle(
            LoginCommand request,
            CancellationToken cancellationToken)
        {
            // The username is logged and the password never is, not even on a
            // failure: a mistyped password is one keystroke away from a real
            // one, so a failed attempt is exactly where a password must not
            // end up in plain text.
            var result = await _identityService.LoginAsync(
                request.Username,
                request.Password,
                cancellationToken);

            if (result.IsError)
            {
                _logger.LogWarning(
                    "Failed sign-in for {Username}: {ErrorCode}.",
                    request.Username,
                    result.FirstError.Code);

                return result;
            }

            _logger.LogInformation("Signed in {Username}.", request.Username);

            return result;
        }
    }
}
