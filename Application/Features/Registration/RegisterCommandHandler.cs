using Application.DTOs;
using Application.Features.Registration;
using Application.ServiceInterfaces;
using ErrorOr;
using Hangfire;
using MediatR;

public class RegisterCommandHandler
    : IRequestHandler<RegisterCommand, ErrorOr<RegisteredUserDTO>>
{
    private readonly IIdentityService _identityService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public RegisterCommandHandler(
        IIdentityService identityService,
         IBackgroundJobClient backgroundJobClient)
    {
        _identityService = identityService;
        _backgroundJobClient = backgroundJobClient;
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

        if (user == null)
        {
            return Error.Failure(
                "failed to create user",
                "a failure has occurred");
        }
        _backgroundJobClient.Enqueue(() =>

        Console.WriteLine($"User {user.Username} " +
        $"has been registered successfully."));



        return user;
    }
}