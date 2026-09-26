namespace Application.DTOs
{
    public record ResetPasswordDTO(
        string Email,
        string Code,
        string NewPassword);
}
