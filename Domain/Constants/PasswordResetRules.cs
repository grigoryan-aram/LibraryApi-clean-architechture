namespace LibraryApi.Domain.Constants;

/// <summary>
/// Shape and lifetime of the code emailed by the password reset flow.
/// </summary>
public static class PasswordResetRules
{
    /// <summary>
    /// Characters a generated code is drawn from. Mixed case and digits, so a
    /// code reads like <c>VK35oeQ</c>.
    /// </summary>
    public const string CodeAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public const int CodeLength = 7;

    /// <summary>
    /// How long a code stays usable. Short enough to limit the window, long
    /// enough to survive a slow mail server.
    /// </summary>
    public const int ExpiryMinutes = 15;

    /// <summary>
    /// Wrong guesses allowed against one code before it is dead. This, not the
    /// request rate limiter, is what makes the code infeasible to brute force:
    /// the limiter still allows tens of thousands of attempts a day per
    /// address, and addresses are cheap.
    /// </summary>
    public const int MaxAttempts = 5;
}
