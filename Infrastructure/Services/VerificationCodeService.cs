using System.Security.Cryptography;
using System.Text;
using Application.ServiceInterfaces;
using LibraryApi.Domain.Constants;

namespace Infrastructure.Services
{
    public class VerificationCodeService : IVerificationCodeService
    {
        public string Generate()
        {
            var alphabet = PasswordResetRules.CodeAlphabet;
            var code = new char[PasswordResetRules.CodeLength];

            for (var i = 0; i < code.Length; i++)
            {
                // RandomNumberGenerator, not Random: Random is predictable from
                // a handful of outputs, which would make a code guessable
                // rather than merely improbable. GetInt32 is also rejection
                // sampled, so no character is favoured by a modulo bias.
                code[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }

            return new string(code);
        }

        public string Hash(string code) =>
            Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code))));

        public bool Verify(string code, string hash)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(Hash(code)),
                Encoding.UTF8.GetBytes(hash));
        }

        // Upper-cased before hashing, so the comparison ignores case and a
        // mail client or phone keyboard that capitalised the code still works.
        // It costs entropy — 36^7 rather than 62^7 — but 78 billion
        // combinations against five allowed guesses is not the weak link.
        private static string Normalize(string code) =>
            code.Trim().ToUpperInvariant();
    }
}
