using Application.Features.Registration;
using LibraryApi.Application.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LibraryApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly MediatR.ISender _sender;
        private readonly SignInManager<IdentityUser> _signInManager;

        public AuthController(
            MediatR.ISender sender,
            SignInManager<IdentityUser> signInManager)
        {
            _sender = sender;
            _signInManager = signInManager;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDTO request)
        {
            await _sender.Send(new RegisterCommand(
                request.Username,
                request.Password,
                request.Email));

            return Ok(new
            {
                Message = "User registered successfully"
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDTO dto)
        {
            var result = await _signInManager.PasswordSignInAsync(
                dto.Username,
                dto.Password,
                isPersistent: false,
                lockoutOnFailure: false);

            if (!result.Succeeded)
                return Unauthorized("Invalid login");

            return Ok("Login successful");
        }
    }
}