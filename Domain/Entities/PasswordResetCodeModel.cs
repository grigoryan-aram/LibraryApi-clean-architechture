namespace LibraryApi.Domain.Entities;

public class PasswordResetCodeModel
{
    public int Id { get; set; }

    public string IdentityUserId { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 of the upper-cased code, never the code itself, so a leaked row
    /// does not hand over the account.
    /// </summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public int AttemptCount { get; set; }
}
