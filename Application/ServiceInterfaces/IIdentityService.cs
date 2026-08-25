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
    }
}
