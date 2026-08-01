
using Application.DTOs;
using Application.ServiceInterfaces;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Services
{
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<IdentityUser> _userManager;

        public IdentityService(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<RegisteredUserDTO> RegisterAsync(
            string username,
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            var user = new IdentityUser
            {
                UserName = username,
                Email = email
            };

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                return null!;
            }

            return new RegisteredUserDTO
            {
                Username = user.UserName,
                Email = user.Email
            };
        }
    }

}