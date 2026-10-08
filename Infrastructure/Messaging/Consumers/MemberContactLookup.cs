using Application.DTOs;
using Application.ServiceInterfaces;
using LibraryApi.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging.Consumers
{
    /// <summary>
    /// Who to email about a member's loan, if anyone.
    /// </summary>
    /// <remarks>
    /// A member and an account are separate: walk-ins added through
    /// <c>POST /api/Members</c> have no account and so no address. That is the
    /// normal case for them, not a failure, so it answers null and the
    /// consumer acknowledges the message rather than retrying it.
    /// </remarks>
    public class MemberContactLookup
    {
        private readonly IMembersRepository _members;
        private readonly IIdentityService _identity;
        private readonly ILogger<MemberContactLookup> _logger;

        public MemberContactLookup(
            IMembersRepository members,
            IIdentityService identity,
            ILogger<MemberContactLookup> logger)
        {
            _members = members;
            _identity = identity;
            _logger = logger;
        }

        public async Task<AccountContactDTO?> FindAsync(
            int memberId,
            CancellationToken cancellationToken)
        {
            var member = await _members.GetMemberByIdAsync(memberId, cancellationToken);

            if (member is null)
            {
                _logger.LogWarning(
                    "Member {MemberId} no longer exists; nobody to notify.",
                    memberId);

                return null;
            }

            if (member.IdentityUserId is null)
            {
                _logger.LogInformation(
                    "Member {MemberId} has no account and so no email address.",
                    memberId);

                return null;
            }

            var contact = await _identity.FindContactAsync(
                member.IdentityUserId,
                cancellationToken);

            if (contact is null)
            {
                _logger.LogWarning(
                    "Member {MemberId} is linked to account {IdentityUserId}, " +
                    "which has no email address.",
                    memberId,
                    member.IdentityUserId);
            }

            return contact;
        }
    }
}
