using Hangfire.Dashboard;

namespace LibraryApi.Extensions
{
    // Without this the dashboard is open to anyone who can reach the URL —
    // it lists every job's arguments, which here include registered users'
    // email addresses, and it can requeue or delete them. Being signed in is
    // not enough: anyone can create an account through /api/Auth/register, so
    // this asks for the Admin role.
    //
    // Hangfire's dashboard has no notion of challenge or redirect — a false
    // here is a flat 401, for an anonymous visitor and a signed-in non-admin
    // alike.
    public class HangfireDashboardAuthorization : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            return AdminAccess.IsAdmin(httpContext.User);
        }
    }
}
