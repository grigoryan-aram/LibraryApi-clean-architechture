using Application.ServiceInterfaces;
using ErrorOr;
using LibraryApi.Domain.Constants;
using LibraryApi.Domain.RepositoryInterfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.PasswordReset
{
    public class ResetPasswordCommandHandler
        : IRequestHandler<ResetPasswordCommand, ErrorOr<Success>>
    {
        private readonly IIdentityService _identityService;
        private readonly IPasswordResetCodeRepository _codes;
        private readonly IVerificationCodeService _verificationCodes;
        private readonly ILogger<ResetPasswordCommandHandler> _logger;

        public ResetPasswordCommandHandler(
            IIdentityService identityService,
            IPasswordResetCodeRepository codes,
            IVerificationCodeService verificationCodes,
            ILogger<ResetPasswordCommandHandler> logger)
        {
            _identityService = identityService;
            _codes = codes;
            _verificationCodes = verificationCodes;
            _logger = logger;
        }

        // One error for every way the code can fail: unknown address, no code
        // issued, expired, too many guesses, or simply wrong. Distinguishing
        // them would tell an unauthenticated caller which addresses have
        // accounts and which codes are live.
        private static Error InvalidCode => Error.Validation(
            "PasswordReset.InvalidCode",
            "That code is not valid. Request a new one and try again.");

        public async Task<ErrorOr<Success>> Handle(
            ResetPasswordCommand request,
            CancellationToken cancellationToken)
        {
            var user = await _identityService.FindByEmailAsync(
                request.Email,
                cancellationToken);

            if (user is null)
            {
                return InvalidCode;
            }

            var stored = await _codes.GetActiveAsync(
                user.UserId,
                DateTime.UtcNow,
                cancellationToken);

            if (stored is null)
            {
                return InvalidCode;
            }

            if (stored.AttemptCount >= PasswordResetRules.MaxAttempts)
            {
                _logger.LogWarning(
                    "Password reset code for {Username} is spent after {Attempts} attempts.",
                    user.Username,
                    stored.AttemptCount);

                return InvalidCode;
            }

            if (!_verificationCodes.Verify(request.Code, stored.CodeHash))
            {
                stored.AttemptCount++;
                await _codes.UpdateAsync(stored, cancellationToken);

                _logger.LogWarning(
                    "Wrong password reset code for {Username} ({Attempts} of {MaxAttempts}).",
                    user.Username,
                    stored.AttemptCount,
                    PasswordResetRules.MaxAttempts);

                return InvalidCode;
            }

            var reset = await _identityService.ResetPasswordAsync(
                user.UserId,
                request.NewPassword,
                cancellationToken);

            if (reset.IsError)
            {
                // Identity's own complaints about the new password — too
                // short, no digit. The code stays usable so the caller can
                // retry with a better password rather than starting over.
                return reset.Errors;
            }

            await _codes.DeleteAllForUserAsync(user.UserId, cancellationToken);

            _logger.LogInformation(
                "Password reset completed for {Username}.",
                user.Username);

            return Result.Success;
        }
    }
}
