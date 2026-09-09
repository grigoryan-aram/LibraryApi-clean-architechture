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
using Microsoft.AspNetCore.Cors.Infrastructure;


var builder = WebApplication.CreateBuilder(args);


if (builder.Environment.IsProduction())
{
    builder.Configuration
        .AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(2);
        limiterOptions.QueueLimit = 10;
    });
});

builder.Services.AddApplication();

var infrastructure = builder.Services.AddInfrastructure(builder.Configuration);

if (infrastructure.IsError)
{
    foreach (var error in infrastructure.Errors)
    {
        Console.Error.WriteLine($"Cannot start: [{error.Code}] {error.Description}");
    }

   
    Environment.ExitCode = 1;
    return;
}
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
        // CORS is only needed for the production host, which serves the API
        app.UseCors(CorsOptions =>
        {
            CorsOptions.AllowAnyOrigin();
            CorsOptions.AllowAnyHeader();
            CorsOptions.AllowAnyMethod();
        });

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

