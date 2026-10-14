using Application.ServiceInterfaces;
using ErrorOr;
using LibraryApi.Domain.Constants;
using LibraryApi.Domain.RepositoryInterfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Accounts
{
    /// <summary>
    /// Deletes the caller's own account and signs them out.
    /// </summary>
    /// <remarks>
    /// The member row is kept, not deleted: its loans are the library's record
    /// of where its books went. It is unlinked and renamed instead, so nothing
    /// left behind identifies the person.
    /// </remarks>
    public class DeleteAccountCommandHandler
        : IRequestHandler<DeleteAccountCommand, ErrorOr<Deleted>>
    {
        private readonly IIdentityService _identityService;
        private readonly IMembersRepository _members;
        private readonly ILoansRepository _loans;
        private readonly IPasswordResetCodeRepository _resetCodes;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeleteAccountCommandHandler> _logger;

        public DeleteAccountCommandHandler(
            IIdentityService identityService,
            IMembersRepository members,
            ILoansRepository loans,
            IPasswordResetCodeRepository resetCodes,
            IUnitOfWork unitOfWork,
            ILogger<DeleteAccountCommandHandler> logger)
        {
            _identityService = identityService;
            _members = members;
            _loans = loans;
            _resetCodes = resetCodes;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<ErrorOr<Deleted>> Handle(
            DeleteAccountCommand request,
            CancellationToken cancellationToken)
        {
            // A signed-in cookie alone is not enough for something this final:
            // a borrowed laptop or a stolen cookie should not be able to do it.
            var password = await _identityService.CheckPasswordAsync(
                request.IdentityUserId,
                request.Password,
                cancellationToken);

            if (password.IsError)
            {
                return password.Errors;
            }

            // The startup seeder recreates the configured administrator on the
            // next start, so deleting it would only appear to work.
            if (await _identityService.IsInRoleAsync(
                    request.IdentityUserId, Roles.Admin, cancellationToken))
            {
                return Error.Conflict(
                    "Account.AdminCannotBeDeleted",
                    "The administrator account is managed by the server's " +
                    "configuration and cannot be deleted here.");
            }

            var member = await _members.GetMemberByIdentityUserIdAsync(
                request.IdentityUserId,
                cancellationToken);

            if (member is not null)
            {
                var loans = await _loans.GetLoansForMemberAsync(member.Id, cancellationToken);
                var booksOut = loans.Count(loan => loan.ReturnedAt is null);

                if (booksOut > 0)
                {
                    return Error.Conflict(
                        "Account.HasOpenLoans",
                        booksOut == 1
                            ? "Return the book you have on loan before deleting your account."
                            : $"Return the {booksOut} books you have on loan before deleting your account.");
                }
            }

            // All or nothing: an account deleted with its member still linked
            // would leave a member pointing at nobody, and a member unlinked
            // from an account that survived would orphan that person's history.
            var deleted = await _unitOfWork.ExecuteInTransactionAsync<Deleted>(async token =>
            {
                if (member is not null)
                {
                    member.IdentityUserId = null;
                    member.Name = $"Former member #{member.Id}";
                    await _members.UpdateMemberAsync(member, token);
                }

                await _resetCodes.DeleteAllForUserAsync(request.IdentityUserId, token);

                return await _identityService.DeleteAccountAsync(request.IdentityUserId, token);
            }, cancellationToken);

            if (deleted.IsError)
            {
                _logger.LogError(
                    "Could not delete account {IdentityUserId}: {ErrorCode}.",
                    request.IdentityUserId,
                    deleted.FirstError.Code);

                return deleted.Errors;
            }

            // After the commit. The cookie would no longer authenticate anyway —
            // its user is gone — but leaving it would look signed in until the
            // security stamp check next ran.
            await _identityService.SignOutAsync();

            _logger.LogInformation(
                "Deleted account {IdentityUserId}; member {MemberId} kept and unlinked.",
                request.IdentityUserId,
                member?.Id);

            return Result.Deleted;
        }
    }
}
