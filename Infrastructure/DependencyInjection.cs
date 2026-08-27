using Application.RepositoryInterfaces;
using Application.ServiceInterfaces;
using ErrorOr;
using FluentEmail.MailKitSmtp;
using Hangfire;
using Infrastructure.Identity;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Infrastructure.Settings;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace Infrastructure.DependencyInjection
{
    public static class DependencyInjection
    {

        // Returns ErrorOr rather than IServiceCollection: a misconfigured app
        // is a real, expected outcome, and the caller decides what to do about
        // it. Nothing chained off the old return value anyway. Program.cs
        // reports the error and stops before building the host.
        public static ErrorOr<Success> AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // No connection string is committed any more. Report it here,
            // early, rather than failing somewhere deep inside EF at first
            // query.
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return Error.Failure(
                    "Infrastructure.MissingConnectionString",
                    "No connection string configured. Set it as the user secret " +
                    "\"ConnectionStrings:DefaultConnection\" for local work, or as " +
                    "the environment variable ConnectionStrings__DefaultConnection " +
                    "on the host.");
            }

            services.AddDbContext<LibraryDBContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<LibraryDBContext>()
            .AddDefaultTokenProviders();

            services.AddScoped<IBooksRepository, BooksRepository>();
            services.AddScoped<ICategorysRepository, CategorysRepository>();
            services.AddScoped<IMembersRepository, MembersRepository>();
            services.AddScoped<ILoansRepository, LoansRepository>();
            services.AddScoped<IIdentityService, IdentityService>();

            // Roles and the seed administrator, created once per start (see
            // IdentitySeeder). Program.cs runs it right after Migrate().
            services.Configure<AdminAccountSettings>(
                configuration.GetSection("Identity:Admin"));

            services.AddScoped<IdentitySeeder>();

            services.AddScoped<IEmailService, EmailService>();

            // The loan period. Stateless, so a singleton is enough.
            //
            // AddLoanCommandHandler takes ILoanPolicy, and Development
            // validates the container at startup — so removing these two lines
            // does not fail the build, it stops the app booting at all.
            services.Configure<LoanSettings>(configuration.GetSection("Loans"));
            services.AddSingleton<ILoanPolicy, ConfiguredLoanPolicy>();

            // Singletons: ClaudeService owns one AnthropicClient (and with it
            // one HttpClient) for the lifetime of the app, and the chat history
            // store is only as long-lived as the IMemoryCache behind it.
            services.AddMemoryCache();
            services.AddSingleton<IChatHistoryStore, InMemoryChatHistoryStore>();
            services.AddSingleton<IClaudeService, ClaudeService>();
            services.AddSingleton<IAiUsageLimiter, InMemoryAiUsageLimiter>();

            services.Configure<ClaudeSettings>(settings =>
            {
                configuration.GetSection("Claude").Bind(settings);

                // appsettings.json ships with an empty key on purpose — no
                // secrets in source control. Fall back to the environment
                // variable the Anthropic SDK and CLI already use.
                if (string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    settings.ApiKey =
                        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                        ?? string.Empty;
                }
            });



            services.AddHangfire(config =>
    config.UseSqlServerStorage(
        configuration.GetConnectionString("DefaultConnection")));
            services.AddHangfireServer();





            services.Configure<EmailSettings>(
            configuration.GetSection("Email"));

            var emailSection = configuration.GetSection("Email");

            // int.Parse here threw a FormatException — or a
            // NullReferenceException through the ! — on a missing or misspelt
            // port, from inside DI registration, where the stack trace says
            // nothing about which setting was wrong.
            if (!int.TryParse(emailSection["Smtp:Port"], out var smtpPort))
            {
                return Error.Failure(
                    "Infrastructure.InvalidSmtpPort",
                    $"Email:Smtp:Port must be a number. It is currently " +
                    $"\"{emailSection["Smtp:Port"]}\".");
            }

            services
                .AddFluentEmail(emailSection["From"])
                .AddMailKitSender(new SmtpClientOptions
                {
                    Server = emailSection["Smtp:Server"],
                    Port = smtpPort,
                    UseSsl = true,
                    RequiresAuthentication = true,
                    User = emailSection["User"],
                    Password = emailSection["Password"]
                });

            return Result.Success;
        }

    }
}
