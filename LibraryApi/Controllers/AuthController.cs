using Application.Features.Registration;
using LibraryApi.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LibraryApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly SignInManager<IdentityUser> _signInManager;

        public AuthController(
            IMediator mediator,
            SignInManager<IdentityUser> signInManager)
        {
            _mediator = mediator;
            _signInManager = signInManager;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDTO request)
        {
            var result = await _mediator.Send(new RegisterCommand(
                request.Username,
                request.Password,
                request.Email));

            return result.Match(
                _ => Ok(new
                {
                    Message = "User registered successfully"
                }),
                errors => Problem(title: errors.First().Description));
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
            {
                return Problem(
                    title: "Invalid login",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Ok(new
            {
                Message = "Login successful"
            });
        }


    }
}