using Application.Jobs;
using Application.ServiceInterfaces;
using ErrorOr;
using Hangfire;
using LibraryApi.Domain.Constants;
using LibraryApi.Domain.Entities;
using LibraryApi.Domain.RepositoryInterfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.PasswordReset
{
    public class ForgotPasswordCommandHandler
        : IRequestHandler<ForgotPasswordCommand, ErrorOr<Success>>
    {
        private readonly IIdentityService _identityService;
        private readonly IPasswordResetCodeRepository _codes;
        private readonly IVerificationCodeService _verificationCodes;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ILogger<ForgotPasswordCommandHandler> _logger;

        public ForgotPasswordCommandHandler(
            IIdentityService identityService,
            IPasswordResetCodeRepository codes,
            IVerificationCodeService verificationCodes,
            IBackgroundJobClient backgroundJobClient,
            ILogger<ForgotPasswordCommandHandler> logger)
        {
            _identityService = identityService;
            _codes = codes;
            _verificationCodes = verificationCodes;
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
        }

        /// <summary>
        /// Always succeeds, whatever the address.
        /// </summary>
        /// <remarks>
        /// Every path here returns Success, including an address with no
        /// account and a mail job that could not be queued. Anything else
        /// turns the endpoint into an oracle for which addresses are
        /// registered — the caller is not authenticated, so a different answer
        /// for a known address is a free account-enumeration tool. The cost is
        /// that a genuine failure to queue is invisible to the caller, so it
        /// is logged at Error.
        /// </remarks>
        public async Task<ErrorOr<Success>> Handle(
            ForgotPasswordCommand request,
            CancellationToken cancellationToken)
        {
            var user = await _identityService.FindByEmailAsync(
                request.Email,
                cancellationToken);

            if (user is null)
            {
                _logger.LogInformation(
                    "Password reset requested for an address with no account.");

                return Result.Success;
            }

            var code = _verificationCodes.Generate();
            var now = DateTime.UtcNow;

            try
            {
                await _codes.DeleteAllForUserAsync(user.UserId, cancellationToken);

                await _codes.AddAsync(
                    new PasswordResetCodeModel
                    {
                        IdentityUserId = user.UserId,
                        CodeHash = _verificationCodes.Hash(code),
                        CreatedAt = now,
                        ExpiresAt = now.AddMinutes(PasswordResetRules.ExpiryMinutes),
                        AttemptCount = 0
                    },
                    cancellationToken);

                _backgroundJobClient.Enqueue<SendPasswordResetEmailJob>(
                    job => job.ExecuteAsync(user.Email, user.Username, code));

                _logger.LogInformation(
                    "Queued a password reset code for {Username}.",
                    user.Username);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Could not issue a password reset code for {Username}.",
                    user.Username);
            }

            return Result.Success;
        }
    }
}
