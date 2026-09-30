namespace Application.ServiceInterfaces
{
    /// <summary>
    /// Generates and checks the short codes emailed by the password reset
    /// flow. An Application-owned abstraction because the handlers live here
    /// and cryptography is an Infrastructure concern — the same shape as
    /// <see cref="ILoanPolicy"/>, and it lets a handler test use a fixed code.
    /// </summary>
    public interface IVerificationCodeService
    {
        string Generate();

        string Hash(string code);

        /// <summary>
        /// True when <paramref name="code"/> matches <paramref name="hash"/>.
        /// Case-insensitive, so a mail client that capitalised the code still
        /// works.
        /// </summary>
        bool Verify(string code, string hash);
    }
}
