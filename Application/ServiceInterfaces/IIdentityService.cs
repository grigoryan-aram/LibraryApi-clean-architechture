using Application.DTOs;

namespace Application.ServiceInterfaces
{
    public interface IIdentityService
    {
        Task<RegisteredUserDTO> RegisterAsync(
            string username,
            string email,
            string password,
            CancellationToken cancellationToken);
    }
}
