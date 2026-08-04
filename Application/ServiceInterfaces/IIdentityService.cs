using Application.DTOs;
using ErrorOr;

namespace Application.ServiceInterfaces
{
    public interface IIdentityService
    {
        Task<RegisteredUserDTO> RegisterAsync(
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
