using Application.DependencyInjection;
using LibraryApi.Components;
using Hangfire;
using Hangfire.Dashboard;
using LibraryApi.Extensions;
using Infrastructure.DependencyInjection;
using LibraryApi.Infrastructure.Data;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddInfrastructure(builder.Configuration);


// Identity defaults to /Account/Login, which does not exist here — a browser
// hitting a [Authorize] page was being sent to a 404. Point it at the Blazor
// account page instead. API callers are unaffected: the cookie handler still
// answers 401 when the request does not accept HTML.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account";
    options.LogoutPath = "/account";
    options.AccessDeniedPath = "/account";

    // The auth cookie is the whole session — never let it travel in the clear
    // outside local development, and keep it away from script.
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});




var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRateLimiter();


app.UseMiddleware<GlobalExceptionMiddleware>();


app.UseAuthentication();
app.UseAuthorization();

// Deliberately AFTER authentication: mapped before it, the dashboard was
// reachable by anyone who knew the URL.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthorization()]
});

// Antiforgery has to sit after authentication and before the component
// endpoints — the sign-in and register forms on /account are static-SSR posts.
app.UseAntiforgery();


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
}


app.Run();

