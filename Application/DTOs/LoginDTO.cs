namespace Application.DTOs
{
    public record LoginDTO(
        string Username,
        string Password,
        bool RememberMe = false);
}
