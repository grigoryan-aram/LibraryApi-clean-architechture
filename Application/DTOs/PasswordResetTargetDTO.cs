namespace Application.DTOs
{
    public record PasswordResetTargetDTO(
        string UserId,
        string Username,
        string Email);
}
