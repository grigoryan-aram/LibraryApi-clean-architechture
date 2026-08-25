using Hangfire.Dashboard;

namespace LibraryApi.Extensions
{
    // Without this the dashboard is open to anyone who can reach the URL —
    // it lists every job's arguments, which here include registered users'
    // email addresses, and it can requeue or delete them.
    public class HangfireDashboardAuthorization : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            return httpContext.User.Identity?.IsAuthenticated == true;
        }
    }
}
