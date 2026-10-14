using Application.DTOs;
using Application.Features.Accounts;
using Application.Features.Login.Commands;
using Application.Features.PasswordReset;
using Application.Features.Registration;
using LibraryApi.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;


namespace LibraryApi.Controllers;

    [EnableCors]
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO request)
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
                 errors => this.ToProblem(errors));
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            var result = await _mediator.Send(new LoginCommand(
                dto.Username,
                dto.Password,
                dto.RememberMe));

            return result.Match(
                response => Ok(new
                {
                    Message = response.Message
                }),
                errors => this.ToProblem(errors));
        }

        /// <summary>
        /// Emails a one-time code to the address, if it has an account.
        /// </summary>
        /// <remarks>
        /// Answers the same way whatever the address, so it cannot be used to
        /// discover which addresses are registered.
        /// </remarks>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO request)
        {
            var result = await _mediator.Send(new ForgotPasswordCommand(request.Email));

            return result.Match(
                _ => Ok(new
                {
                    Message = "If that address has an account, a reset code is on its way."
                }),
                errors => this.ToProblem(errors));
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO request)
        {
            var result = await _mediator.Send(new ResetPasswordCommand(
                request.Email,
                request.Code,
                request.NewPassword));

            return result.Match(
                _ => Ok(new
                {
                    Message = "Password changed. Sign in with your new password."
                }),
                errors => this.ToProblem(errors));
        }

        /// <summary>
        /// Changes the signed-in caller's password, given the current one.
        /// </summary>
        /// <remarks>
        /// This session stays signed in; the account's other sessions are
        /// signed out within a minute.
        /// </remarks>
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO request)
        {
            // From the cookie, never the body: see ChangePasswordCommand.
            var identityUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var result = await _mediator.Send(new ChangePasswordCommand(
                identityUserId,
                request.CurrentPassword,
                request.NewPassword));

            return result.Match(
                _ => Ok(new
                {
                    Message = "Password changed."
                }),
                errors => this.ToProblem(errors));
        }

        /// <summary>
        /// Deletes the signed-in caller's own account and signs them out.
        /// </summary>
        /// <remarks>
        /// Refused while any book is still on loan. Past loans are kept, on a
        /// member record that no longer names the account.
        /// </remarks>
        [Authorize]
        [HttpPost("delete-account")]
        public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountDTO request)
        {
            // From the cookie, never the body: see DeleteAccountCommand.
            var identityUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var result = await _mediator.Send(new DeleteAccountCommand(
                identityUserId,
                request.Password));

            return result.Match(
                _ => Ok(new
                {
                    Message = "Your account has been deleted."
                }),
                errors => this.ToProblem(errors));
        }
    }
