
using Application.DTOs;
using Application.ServiceInterfaces;
using ErrorOr;
using LibraryApi.Domain.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<IdentityService> _logger;

        public IdentityService(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ILogger<IdentityService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
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

        public async Task<ErrorOr<RegisteredUserDTO>> RegisterAsync(
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
                // Identity's own messages are the only thing that explains a
                // refused registration — a weak password, a taken username, a
                // duplicate email. Pass every one of them through instead of
                // flattening them into a single opaque failure.
                return result.Errors
                    .Select(error => Error.Validation(
                        code: $"Identity.{error.Code}",
                        description: error.Description))
                    .ToList();
            }

            // Everyone who registers is a plain User. Admin is granted only by
            // the startup seeder, so there is no path from the public
            // registration endpoint to the Swagger or Hangfire dashboards.
            var role = await _userManager.AddToRoleAsync(user, Roles.User);

            if (!role.Succeeded)
            {
                // The account already exists by now. Refusing here would tell
                // the caller registration failed while their username was in
                // fact taken — by them. Log it instead: an account with no
                // role can still use everything but the two dashboards.
                _logger.LogError(
                    "Registered {Username} but could not grant the {Role} role: {Errors}",
                    user.UserName,
                    Roles.User,
                    string.Join("; ", role.Errors.Select(error => error.Description)));
            }

            return new RegisteredUserDTO(

             user.UserName!,
             user.Email!);
        }
    }

}