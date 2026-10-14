using Application.ServiceInterfaces;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Accounts
{
    public class ChangePasswordCommandHandler
        : IRequestHandler<ChangePasswordCommand, ErrorOr<Success>>
    {
        private readonly IIdentityService _identityService;
        private readonly ILogger<ChangePasswordCommandHandler> _logger;

        public ChangePasswordCommandHandler(
            IIdentityService identityService,
            ILogger<ChangePasswordCommandHandler> logger)
        {
            _identityService = identityService;
            _logger = logger;
        }

        public async Task<ErrorOr<Success>> Handle(
            ChangePasswordCommand request,
            CancellationToken cancellationToken)
        {
            // Identity checks the current password and the new one's strength
            // in one call, and rotates the security stamp on success — which
            // signs out the account's other cookies within a minute.
            var changed = await _identityService.ChangePasswordAsync(
                request.IdentityUserId,
                request.CurrentPassword,
                request.NewPassword,
                cancellationToken);

            if (changed.IsError)
            {
                // The code only, never either password.
                _logger.LogWarning(
                    "Refused a password change for {IdentityUserId}: {ErrorCode}.",
                    request.IdentityUserId,
                    changed.FirstError.Code);

                return changed.Errors;
            }

            _logger.LogInformation(
                "Changed the password for {IdentityUserId}.",
                request.IdentityUserId);

            return Result.Success;
        }
    }
}
