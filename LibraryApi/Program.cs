using Application.DependencyInjection;
using Hangfire;
using Infrastructure.DependencyInjection;
using Infrastructure.Identity;
using LibraryApi.Components;
using LibraryApi.Extensions;
using LibraryApi.MiddleWares;
using LibraryApi.Infrastructure.Data;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

// The host (MonsterASP shared IIS) has no place to set environment variables,
// so production credentials live in appsettings.Secrets.json. That file is
// gitignored and copied to the server by publishing (see the Content item in
// Presentation.csproj), so it is never committed and never hand-uploaded.
//
// Production ONLY, deliberately: the file holds the production connection
// string, and loading it locally would silently point `dotnet run` — and the
// Database.Migrate() call at the bottom of this file — at the live database.
// Local development gets these values from user secrets instead.
//
// Environment variables are re-added afterwards so they still win on a host
// that does support them.
if (builder.Environment.IsProduction())
{
    builder.Configuration
        .AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();
}

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// Blazor Server: interactive components run on a SignalR circuit (WebSocket),
// so the pages can call IMediator in-process instead of going back out over
// HTTP to this same app.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

// SignInManager reaches for the current request through IHttpContextAccessor
// when it is used outside a controller, which is what Account.razor does.
builder.Services.AddHttpContextAccessor();



builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(5);
        limiterOptions.QueueLimit = 1;
    });
});






builder.Services.AddApplication();

// AddInfrastructure reports a misconfiguration instead of throwing one, so
// stopping is this file's job. There is no host and no ILogger yet, hence
// stderr; and it happens before Build(), so nothing has opened a database
// connection or started a Hangfire worker by the time we quit.
var infrastructure = builder.Services.AddInfrastructure(builder.Configuration);

if (infrastructure.IsError)
{
    foreach (var error in infrastructure.Errors)
    {
        Console.Error.WriteLine($"Cannot start: [{error.Code}] {error.Description}");
    }

    // Non-zero, so a process manager or CI step sees a failed start rather
    // than a clean exit.
    Environment.ExitCode = 1;
    return;
}


// Identity defaults to /Account/Login, which does not exist here — a browser
// hitting a [Authorize] page was being sent to a 404. Point it at the Blazor
// account page instead. API callers are unaffected: the cookie handler still
// answers 401 when the request does not accept HTML.
// Whether this deployment can actually serve TLS. It is TRUE everywhere by
// default and false only where a config file says so — currently the
// production host, whose free plan does not offer a certificate, so the site
// is served over plain HTTP as a deliberate and recorded decision.
//
// While it is false, login passwords, the session cookie and the Blazor
// WebSocket all cross the network in the clear, and anyone sharing a network
// with a user can take over their session. Flip it back to true the day the
// host has a certificate: it turns the redirect, HSTS and the cookie's Secure
// flag back on together.
var requireHttps = builder.Configuration.GetValue("Security:RequireHttps", true);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account";
    options.LogoutPath = "/account";
    options.AccessDeniedPath = "/account";

    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    // Secure would make the cookie unusable on an HTTP-only host: the browser
    // would accept it and never send it back, so sign-in would fail with no
    // error to explain why.
    options.Cookie.SecurePolicy = requireHttps && !builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
});




var app = builder.Build();


if (requireHttps)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
}
else
{
    // Loud on every start, so an HTTP-only deployment can never be a thing
    // somebody forgot about.
    app.Logger.LogWarning(
        "Security:RequireHttps is false — this instance is served over plain " +
        "HTTP. Credentials and session cookies are not encrypted in transit. " +
        "Set it back to true once the host has a TLS certificate.");
}

app.UseRateLimiter();


app.UseMiddleware<GlobalExceptionMiddleware>();


app.UseAuthentication();
app.UseAuthorization();

// Deliberately AFTER authentication: mapped before it, the dashboard was
// reachable by anyone who knew the URL. The filter now asks for the Admin
// role — merely being signed in is no bar at all when anyone can register.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthorization()]
});

// Antiforgery has to sit after authentication and before the component
// endpoints — the sign-in and register forms on /account are static-SSR posts.
app.UseAntiforgery();


// Swagger is served by middleware rather than an endpoint, so there is no
// route to put [Authorize(Roles = "Admin")] on — the path is gated instead.
// Anything under /swagger, including swagger.json, goes through this.
app.UseAdminOnlyPath("/swagger");

app.UseSwagger();
app.UseSwaggerUI();



app.MapStaticAssets();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDBContext>();

    dbContext.Database.Migrate();

    // Roles and the seed administrator. Has to come after Migrate() — it
    // writes to the Identity tables the migrations create — and it is
    // idempotent, so it runs on every start.
    await scope.ServiceProvider
        .GetRequiredService<IdentitySeeder>()
        .SeedAsync();
}


app.Run();

