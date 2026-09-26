using Application.DependencyInjection;
using Hangfire;
using Infrastructure;
using Infrastructure.DependencyInjection;
using Infrastructure.Identity;
using LibraryApi.Components;
using LibraryApi.HealthChecks;
using LibraryApi.Extensions;
using LibraryApi.MiddleWares;
using LibraryApi.Infrastructure.Data;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;


    
var builder = WebApplication.CreateBuilder(args);


if (builder.Environment.IsProduction())
{
    builder.Configuration
        .AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();
}

// Serilog replaces the default console-only provider. Levels come from the
// "Serilog" section; the rolling file is what makes the handler logging
// usable on a host where nobody can watch stdout.
builder.Services.AddSerilog((services, logging) => logging
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(builder.Environment.ContentRootPath, "logs", "library-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        // SourceContext is the logger category — the handler that wrote the
        // line. Without it the file says what happened but not where, which
        // is most of what a per-handler logger is for.
        outputTemplate:
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: " +
            "{Message:lj}{NewLine}{Exception}",
        // IIS can run more than one worker process against the same folder.
        shared: true));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddRateLimiter(options =>
{
    // 429, not the default 503: the caller sent too many requests, the
    // service is not unavailable.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Partitioned per caller. AddFixedWindowLimiter would give every client
    // in the world one shared bucket of 100, so a single busy caller could
    // lock everyone else out. Signed-in callers are keyed by name; everyone
    // else by remote address.
    options.AddPolicy("fixed", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(2),
                // No queue: a rate-limited caller gets an immediate 429 rather
                // than a request held open for up to a whole window. Queuing
                // suits a worker draining a backlog, not an HTTP API.
                QueueLimit = 0
            }));
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

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
    options.LoginPath = "/login";
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

// Unconditional, because [EnableCors] on the controllers is unconditional.
// Nested inside "if (requireHttps) / if (!IsDevelopment())" it never ran in
// Production — Security:RequireHttps is false there — and an endpoint that
// carries CORS metadata with no CORS middleware in the pipeline makes
// EndpointMiddleware throw, which turned every API call into a 500.
app.UseCors(cors =>
{
    cors.AllowAnyOrigin();
    cors.AllowAnyHeader();
    cors.AllowAnyMethod();
});


app.UseMiddleware<GlobalExceptionMiddleware>();


app.UseAuthentication();
app.UseAuthorization();

// After authentication on purpose: the policy partitions on the signed-in
// user name, which is not populated until UseAuthentication has run.
app.UseRateLimiter();

app.UseSerilogRequestLogging();

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
app.MapControllers().RequireRateLimiting("fixed");

// Anonymous on purpose: a probe that needs a cookie cannot be used by the
// host. It reports reachability only, never a connection string or a version.
app.MapHealthChecks("/health");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDBContext>();

    dbContext.Database.Migrate();

    // After Migrate(), which is what creates the database on a first run.
    // Hangfire can no longer install this itself — see the comment on
    // PrepareSchemaIfNecessary in AddInfrastructure.
    //
    // Logged rather than fatal: a host whose SQL login has no DDL rights can
    // still serve every request, and registration already degrades when the
    // enqueue fails. A silent failure here would leave background jobs broken
    // forever with nothing to explain it, so it is loud.
    try
    {
        HangfireSchema.EnsureInstalled(dbContext.Database.GetDbConnection());
    }
    catch (Exception exception)
    {
        app.Logger.LogError(
            exception,
            "Could not install the Hangfire schema. Background jobs will fail " +
            "until this is resolved.");
    }

    // Roles and the seed administrator. Has to come after Migrate() — it
    // writes to the Identity tables the migrations create — and it is
    // idempotent, so it runs on every start.
    await scope.ServiceProvider
        .GetRequiredService<IdentitySeeder>()
        .SeedAsync();
}


app.Run();

