using Application.ServiceInterfaces;
using ErrorOr;
using FluentEmail.MailKitSmtp;
using Hangfire;
using MassTransit;
using Hangfire.SqlServer;
using Infrastructure.Identity;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Consumers;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Infrastructure.Settings;
using LibraryApi.Domain.RepositoryInterfaces;
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
            services.AddScoped<IPasswordResetCodeRepository, PasswordResetCodeRepository>();

            // Stateless, so a singleton is enough.
            services.AddSingleton<IVerificationCodeService, VerificationCodeService>();

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


            // PrepareSchemaIfNecessary is off on purpose. Left on, Hangfire
            // installs its schema while services are still being registered —
            // before Program.cs calls Database.Migrate() — so against a brand
            // new database the installer fails, gives up after its retries and
            // never runs again, leaving every Enqueue for the life of the
            // process throwing "Invalid object name 'HangFire.Job'". The schema
            // is installed explicitly after Migrate() instead; see
            // HangfireSchema.EnsureInstalled.
            services.AddHangfire(config =>
                config.UseSqlServerStorage(
                    connectionString,
                    new SqlServerStorageOptions
                    {
                        PrepareSchemaIfNecessary = false
                    }));

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

            AddMessaging(services, configuration);

            return Result.Success;
        }

        /// <summary>
        /// Registers the RabbitMQ bus, or a no-op publisher when no broker is
        /// configured.
        /// </summary>
        /// <remarks>
        /// Conditional on purpose. MassTransit's bus is a hosted service that
        /// starts connecting as soon as it is registered and retries forever,
        /// so registering it unconditionally would fill the log of every
        /// deployment that has no RabbitMQ — which is currently all of them.
        /// Publishing integration events is additive; it must not change how a
        /// deployment without a broker behaves.
        /// </remarks>
        private static void AddMessaging(
            IServiceCollection services,
            IConfiguration configuration)
        {
            var rabbit = configuration.GetSection("RabbitMq").Get<RabbitMqSettings>()
                ?? new RabbitMqSettings();

            services.Configure<RabbitMqSettings>(configuration.GetSection("RabbitMq"));

            // Handlers wrap an entity write and its event in one transaction,
            // with or without a broker.
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            if (!rabbit.IsConfigured)
            {
                services.AddSingleton<IEventPublisher, NoOpEventPublisher>();
                return;
            }

            services.AddScoped<MemberContactLookup>();

            services.AddMassTransit(bus =>
            {
                bus.SetKebabCaseEndpointNameFormatter();

                // Bus outbox: a publish from a request writes an OutboxMessage
                // row through the request's DbContext, and the delivery
                // service forwards it to RabbitMQ afterwards. Consumer outbox
                // (the inbox): each endpoint records what it has consumed, so
                // a redelivered message is not handled twice.
                bus.AddEntityFrameworkOutbox<LibraryDBContext>(outbox =>
                {
                    outbox.UseSqlServer();
                    outbox.UseBusOutbox();

                    // Default is 10 seconds. One keeps an emailed receipt
                    // close to the action, at the cost of a cheap query a
                    // second while idle.
                    outbox.QueryDelay = TimeSpan.FromSeconds(1);
                });

                bus.AddConsumer<BookBorrowedConsumer>();
                bus.AddConsumer<BookReturnedConsumer>();

                // Retry before the inbox, which is the order MassTransit
                // documents: each retry then re-runs inside a fresh inbox
                // transaction. A message still failing after these goes to
                // its endpoint's _error queue.
                bus.AddConfigureEndpointsCallback((context, _, endpoint) =>
                {
                    endpoint.UseMessageRetry(retry => retry.Intervals(
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(30)));

                    endpoint.UseEntityFrameworkOutbox<LibraryDBContext>(context);
                });

                bus.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(rabbit.Host, rabbit.Port, rabbit.VirtualHost, host =>
                    {
                        host.Username(rabbit.Username);
                        host.Password(rabbit.Password);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            });

            services.AddScoped<IEventPublisher, OutboxEventPublisher>();
        }

    }
}
