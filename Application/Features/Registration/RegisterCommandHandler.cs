using Application.DTOs;
using Application.IntegrationEvents;
using Application.Features.Registration;
using Application.Jobs;
using Application.ServiceInterfaces;
using ErrorOr;
using Hangfire;
using LibraryApi.Domain.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

public class RegisterCommandHandler
    : IRequestHandler<RegisterCommand, ErrorOr<RegisteredUserDTO>>
{
    private readonly IIdentityService _identityService;
    private readonly IMembersRepository _membersRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IEventPublisher _events;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IIdentityService identityService,
        IMembersRepository membersRepository,
        IBackgroundJobClient backgroundJobClient,
        IEventPublisher events,
        ILogger<RegisterCommandHandler> logger)
    {
        _identityService = identityService;
        _membersRepository = membersRepository;
        _backgroundJobClient = backgroundJobClient;
        _events = events;
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
            // The username, never the password — see LoginCommandHandler.
            _logger.LogWarning(
                "Failed registration for {Username}: {ErrorCode}.",
                request.Username,
                user.FirstError.Code);

            return user.Errors;
        }

        var memberId = await LinkMemberAsync(user.Value, cancellationToken);

        if (memberId is not null)
        {
            await _events.PublishAsync(
                new MemberRegistered(
                    user.Value.Username,
                    user.Value.Email,
                    memberId.Value,
                    DateTime.UtcNow),
                cancellationToken);
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

        _logger.LogInformation(
            "Registered {Username} as user {UserId}.",
            user.Value.Username,
            user.Value.UserId);

        return user.Value;
    }

    private async Task<int?> LinkMemberAsync(
        RegisteredUserDTO user,
        CancellationToken cancellationToken)
    {
        try
        {
            var existing = await _membersRepository.GetMemberByIdentityUserIdAsync(
                user.UserId,
                cancellationToken);

            if (existing is not null)
            {
                return existing.Id;
            }

            var member = await _membersRepository.AddMemberAsync(
                new MemberModel
                {
                    Name = user.Username,
                    IdentityUserId = user.UserId
                },
                cancellationToken);

            return member.Id;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Registered {Username} but could not create their library member.",
                user.Username);

            // Null, not a throw: the account exists and registration has
            // succeeded. It also suppresses MemberRegistered, which carries a
            // member id there would be nothing honest to put in.
            return null;
        }
    }
}
