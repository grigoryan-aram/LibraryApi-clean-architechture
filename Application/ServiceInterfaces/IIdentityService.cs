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
            CancellationToken cancellationToken);

        // Null rather than an error for an address with no account: the
        // password reset flow must answer identically either way, so "no such
        // user" is an ordinary result there, not a failure.
        Task<PasswordResetTargetDTO?> FindByEmailAsync(
            string email,
            CancellationToken cancellationToken);

        Task<ErrorOr<Success>> ResetPasswordAsync(
            string identityUserId,
            string newPassword,
            CancellationToken cancellationToken);
    }
}
