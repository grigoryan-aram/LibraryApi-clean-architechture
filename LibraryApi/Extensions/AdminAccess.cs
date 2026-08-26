using System.Security.Claims;
using LibraryApi.Domain.Constants;

namespace LibraryApi.Extensions
{
    /// <summary>
    /// The one place that decides who counts as an administrator. Both gates
    /// on the two dashboards — the Hangfire dashboard filter and the Swagger
    /// middleware — ask this, so they can never drift apart.
    /// </summary>
    public static class AdminAccess
    {
        public static bool IsAuthenticated(ClaimsPrincipal? user) =>
            user?.Identity?.IsAuthenticated == true;

        // Role claims ride in the auth cookie, minted at sign-in. A user who
        // was already signed in when their role changed keeps the old answer
        // until they sign in again.
        public static bool IsAdmin(ClaimsPrincipal? user) =>
            IsAuthenticated(user) && user!.IsInRole(Roles.Admin);
    }
}
