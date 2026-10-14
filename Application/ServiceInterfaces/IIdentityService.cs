using Application.DTOs;
using ErrorOr;

namespace Application.ServiceInterfaces
{
    public interface IIdentityService
    {
        // Returns the real ASP.NET Identity failures ("Passwords must have at
        // least one digit", "Username 'x' is already taken"). This used to
        // return the DTO and hand back null on any failure, which collapsed
        // every cause into one useless "a failure has occurred" and made a
        // broken registration impossible to diagnose on a deployed site.
        Task<ErrorOr<RegisteredUserDTO>> RegisterAsync(
            string username,
            string email,
            string password,
            CancellationToken cancellationToken);

        Task<ErrorOr<LoginResponseDTO>> LoginAsync(
            string username,
            string password,
            bool rememberMe,
            CancellationToken cancellationToken);

        // Null rather than an error for an address with no account: the
        // password reset flow must answer identically either way, so "no such
        // user" is an ordinary result there, not a failure.
        Task<PasswordResetTargetDTO?> FindByEmailAsync(
            string email,
            CancellationToken cancellationToken);

        // Null for an id with no account, or an account with no address.
        Task<AccountContactDTO?> FindContactAsync(
            string identityUserId,
            CancellationToken cancellationToken);

        Task<ErrorOr<Success>> ResetPasswordAsync(
            string identityUserId,
            string newPassword,
            CancellationToken cancellationToken);

        // Unauthorized for a wrong password and for an id with no account
        // alike — the caller is re-confirming who they are, not looking up.
        Task<ErrorOr<Success>> CheckPasswordAsync(
            string identityUserId,
            string password,
            CancellationToken cancellationToken);

        Task<bool> IsInRoleAsync(
            string identityUserId,
            string role,
            CancellationToken cancellationToken);

        Task<ErrorOr<Deleted>> DeleteAccountAsync(
            string identityUserId,
            CancellationToken cancellationToken);

        // Unauthorized for a wrong current password; Identity's own complaints
        // about the new one come back as Validation errors. On success the
        // current request's cookie is re-issued, so the caller stays signed in
        // while their other sessions are not.
        Task<ErrorOr<Success>> ChangePasswordAsync(
            string identityUserId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken);

        // Clears the auth cookie on the current request.
        Task SignOutAsync();
    }
}
