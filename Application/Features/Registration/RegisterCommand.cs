using Application.DTOs;
using ErrorOr;
using MediatR;

namespace Application.Features.Registration
{
    public record RegisterCommand(
    string Username,
    string Password,
    string Email
) : IRequest<ErrorOr<RegisteredUserDTO>>;

}