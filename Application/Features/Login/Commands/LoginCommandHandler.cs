using Application.DTOs;
using Application.ServiceInterfaces;
using ErrorOr;
using MediatR;

namespace Application.Features.Login.Commands
{
    public class LoginCommandHandler
       : IRequestHandler<LoginCommand, ErrorOr<LoginResponseDTO>>
    {
        private readonly IIdentityService _identityService;

        public LoginCommandHandler(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        public Task<ErrorOr<LoginResponseDTO>> Handle(
            LoginCommand request,
            CancellationToken cancellationToken)
        {
            return _identityService.LoginAsync(
                request.Username,
                request.Password,
                cancellationToken);
        }
    }
}
