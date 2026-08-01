using Application.DTOs;
using Application.ServiceInterfaces;
using ErrorOr;
using MediatR;

namespace Application.Features.Registration
{
    public class RegisterCommandHandler : IRequestHandler<RegisterCommand, ErrorOr<RegisteredUserDTO>>
    {
        private readonly IIdentityService _identityService;
        private readonly IPublisher _publisher;

        public RegisterCommandHandler(
            IIdentityService identityService,
            IPublisher publisher)
        {
            _identityService = identityService;
            _publisher = publisher;
        }

        public async Task<ErrorOr<RegisteredUserDTO>> Handle(RegisterCommand request,
            CancellationToken cancellationToken)
        {



            var user = await _identityService.RegisterAsync
                (request.Username
                , request.Email
                , request.Password
                , cancellationToken);


            if (user == null)
            {
                return Error.Failure("failed to create user", "a failure has occurred");
            }


            await _publisher.Publish(
                new UserRegisteredNotification(
                    user.Email,
                    user.Username),
                cancellationToken);

            return user;
        }
    }
}
