namespace Application.DTOs
{
    public record ChangePasswordDTO(
        string CurrentPassword,
        string NewPassword);
}
