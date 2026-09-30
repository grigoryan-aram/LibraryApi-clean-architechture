using LibraryApi.Domain.Entities;

namespace LibraryApi.Domain.RepositoryInterfaces
{
    public interface IPasswordResetCodeRepository
    {
        Task<PasswordResetCodeModel> AddAsync(
            PasswordResetCodeModel code,
            CancellationToken cancellationToken);

        /// <summary>
        /// The newest unexpired code for an account, or null. Issuing a code
        /// clears the previous ones, so at most one is ever live.
        /// </summary>
        Task<PasswordResetCodeModel?> GetActiveAsync(
            string identityUserId,
            DateTime asOf,
            CancellationToken cancellationToken);

        Task<PasswordResetCodeModel> UpdateAsync(
            PasswordResetCodeModel code,
            CancellationToken cancellationToken);

        Task DeleteAllForUserAsync(
            string identityUserId,
            CancellationToken cancellationToken);
    }
}
