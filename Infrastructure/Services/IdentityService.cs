
using Application.DTOs;
using Application.ServiceInterfaces;
using ErrorOr;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Services
{
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public IdentityService(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task<ErrorOr<LoginResponseDTO>> LoginAsync(string username, string password, CancellationToken cancellationToken)
        {
            var result = await _signInManager.PasswordSignInAsync(
                 username,
                 password,
                 isPersistent: false,
                 lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                return Error.Unauthorized(
                    code: "Auth.InvalidCredentials",
                    description: "Invalid username or password.");
            }

            return new LoginResponseDTO("Login successful");
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

            return new RegisteredUserDTO(

             user.UserName!,
             user.Email!);
        }
    }

}