using LibraryApi.Extensions;
using Microsoft.AspNetCore.Authentication;

namespace LibraryApi.MiddleWares
{
    /// <summary>
    /// Closes a path prefix to everyone but administrators.
    ///
    /// This exists because the Swagger UI is not an endpoint: it is served by
    /// <c>UseSwagger()</c>/<c>UseSwaggerUI()</c> middleware, so there is no
    /// route to hang an <c>[Authorize(Roles = ...)]</c> attribute on. Gating
    /// the path is the only way to reach it.
    ///
    /// It must be registered AFTER UseAuthentication(), or the user is still
    /// anonymous when it looks, and BEFORE the middleware it is protecting.
    /// </summary>
    public class AdminOnlyPathMiddleWare
    {
        private readonly RequestDelegate _next;
        private readonly PathString _path;

        public AdminOnlyPathMiddleWare(RequestDelegate next, PathString path)
        {
            _next = next;
            _path = path;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments(_path))
            {
                await _next(context);
                return;
            }

            // Challenge, not a bare 401: the cookie handler turns it into a
            // redirect to /account for a browser and leaves it a 401 for a
            // client that does not accept HTML — the same split the API
            // endpoints already get.
            if (!AdminAccess.IsAuthenticated(context.User))
            {
                await context.ChallengeAsync();
                return;
            }

            // Signed in but not an admin: 403 rather than a login prompt,
            // because signing in again would change nothing.
            if (!AdminAccess.IsAdmin(context.User))
            {
                await context.ForbidAsync();
                return;
            }

            await _next(context);
        }
    }

    public static class AdminOnlyPathMiddleWareExtensions
    {
        public static IApplicationBuilder UseAdminOnlyPath(
            this IApplicationBuilder app, string path) =>
            app.UseMiddleware<AdminOnlyPathMiddleWare>(new PathString(path));
    }
}
