using Application.DTOs;
using Application.Features.Registration;
using Application.Jobs;
using Application.ServiceInterfaces;
using ErrorOr;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;

public class RegisterCommandHandler
    : IRequestHandler<RegisterCommand, ErrorOr<RegisteredUserDTO>>
{
    private readonly IIdentityService _identityService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IIdentityService identityService,
         IBackgroundJobClient backgroundJobClient,
         ILogger<RegisterCommandHandler> logger)
    {
        _identityService = identityService;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task<ErrorOr<RegisteredUserDTO>> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _identityService.RegisterAsync(
            request.Username,
            request.Email,
            request.Password,
            cancellationToken);

        if (user.IsError)
        {
            return user.Errors;
        }

        // The account already exists at this point. If Hangfire cannot take the
        // job — its schema missing on a fresh database, the SQL user lacking
        // rights — that must not turn a successful registration into an error
        // the caller can never get past: they would be told registration failed
        // while their username was in fact taken, by them.
        try
        {
            _backgroundJobClient.Enqueue<SendWelcomeEmailJob>(
                job => job.ExecuteAsync(user.Value.Email, user.Value.Username));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Registered {Username} but could not queue the welcome email.",
                user.Value.Username);
        }

        return user.Value;
    }
}
