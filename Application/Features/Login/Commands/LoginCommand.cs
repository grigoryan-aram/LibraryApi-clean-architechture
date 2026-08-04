using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Login.Commands
{
    public record LoginCommand(
    string Username,
    string Password)
    : IRequest<ErrorOr<LoginResponseDTO>>;
}
