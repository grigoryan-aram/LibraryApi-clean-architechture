# CLAUDE.md

Clean-architecture ASP.NET Core Web API for a library domain (books, categories, members, loans) on .NET 10, using CQRS via MediatR, `ErrorOr` for results, EF Core + SQL Server, ASP.NET Core Identity, and Hangfire for background jobs.

## Solution layout

The solution is **`LibraryApi.slnx`** at the repo root.

| Project | Path | References |
|---|---|---|
| Domain | `Domain/Domain.csproj` | — |
| Application | `Application/Application.csproj` | Domain |
| Infrastructure | `Infrastructure/Infrastructure.csproj` | Application, Domain |
| Presentation (web host) | `LibraryApi/Presentation.csproj` | Infrastructure |

Presentation references only Infrastructure; Application/Domain come in transitively. Keep the inward dependency flow: Domain has no dependencies, Application depends only on Domain plus abstractions it declares itself, Infrastructure implements those abstractions.

Note the mismatch between folder and project name: the web host lives in `LibraryApi/` but its project is `Presentation.csproj` (assembly `Presentation`, `RootNamespace` `LibraryApi`).

### Leftovers from a recent cleanup

The repo used to carry a duplicate set of host files at the root (`Program.cs`, `Presentation.csproj`, `appsettings*.json`, `dotnet-tools.json`, `Library API.http`) plus a second solution file at `LibraryApi/LibraryApi.slnx`. Those are **deleted on disk but the deletions are uncommitted**, and the root `LibraryApi.slnx` rewrite that replaced them is uncommitted too — a stray `git checkout`/`git stash` will resurrect the duplicates. The live host files are all under `LibraryApi/`.

Still-orphaned leftovers, safe to ignore and reasonable to delete: root `Properties/launchSettings.json` (a `Presentation` profile on ports 50522/50523 with no project at the root to own it) and the stale root `bin/`/`obj/`. The live launch profiles are `LibraryApi/Properties/launchSettings.json`.

## Commands

Build (from repo root):

```bash
dotnet build LibraryApi.slnx
```

Run (Swagger UI at `/swagger`, http://localhost:5021 / https://localhost:7157):

```bash
dotnet run --project LibraryApi/Presentation.csproj
```

**EF Core CLI must be run from `LibraryApi/`.** The `dotnet-ef` 10.0.10 pin now lives only in `LibraryApi/dotnet-tools.json`, so `dotnet tool restore` fails at the repo root, and `dotnet ef` there silently falls through to whatever global tool is installed (currently 10.0.9). From `LibraryApi/`:

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add <Name> -p ../Infrastructure/Infrastructure.csproj -s Presentation.csproj
```

```bash
dotnet ef migrations list -p ../Infrastructure/Infrastructure.csproj -s Presentation.csproj
```

Migrations live in Infrastructure; the host is always the startup project.

There is no test project. `dotnet build` is the only automated check.

## Request pipeline

Controller → MediatR → handler → repository/service. Every step is thin and uniform:

1. **Controller** (`LibraryApi/Controllers/`) — `[ApiController]`, `[Route("api/[controller]")]`, injects `IMediator` only. Each action sends a command/query and returns `result.Match(ok => Ok(ok), errors => this.ToProblem(errors))`. No logic in controllers.
2. **Command/query** — a `record` implementing `IRequest<ErrorOr<TDto>>` (or `ErrorOr<Deleted>` for deletes).
3. **`ValidationBehavior<,>`** (`Application/Behaviors/ValidatorBehavior.cs`) — MediatR pipeline behavior, runs all registered `IValidator<TRequest>`s and short-circuits with `Error.Validation` results. Constrained to `where TResponse : IErrorOr`, so **every** MediatR request in this codebase must return `ErrorOr<T>`.
4. **Handler** — injects a repository or service interface, returns `Error.NotFound`/`Error.Failure`/etc. on the sad path instead of throwing.
5. **Mapping** — Mapster convention-based `.Adapt<T>()`. There is no Mapster config/profile; mapping relies on matching member names, so renaming a DTO or entity property silently breaks the mapping.

`ErrorExtensions.ToProblem` (`LibraryApi/Extensions/ErrorExtensions.cs`) maps all-validation error lists to a 400 `ValidationProblemDetails` and **everything else to a 400 as well** — `Error.NotFound` currently surfaces as 400, not 404.

## Adding a feature slice

Features are vertical slices under `Application/Features/<Entity>/Commands|Queries/`. A slice is three files with matching names:

```
Application/Features/Books/Commands/AddBookCommand.cs          // record : IRequest<ErrorOr<BooksDTO>>
Application/Features/Books/Commands/AddBookCommandHandler.cs   // IRequestHandler<...>
Application/Features/Books/Commands/AddBookCommandValidator.cs // AbstractValidator<...>
```

DTOs are `record`s in `Application/DTOs/` (`BooksDTO`, `LoansDTO`, …). Handlers and validators are auto-discovered by assembly scanning in `Application/DependencyInjection.cs` — no manual registration. New repositories/services **do** need a line in `Infrastructure/DependencyInjection.cs`.

## Data access

Repository-per-aggregate. Interfaces in `Application/RepositoryInterfaces/`, EF Core implementations in `Infrastructure/Repositories/`. Conventions in the existing repos: reads use `AsNoTracking()`, deletes use `ExecuteDeleteAsync()` (no load-then-remove, so deleting a missing id is a silent no-op), every method takes a `CancellationToken`.

`LibraryDBContext` (`Infrastructure/Data/LibraryDBContext.cs`) extends `IdentityDbContext<IdentityUser>`, declares relationships inline in `OnModelCreating`, then calls `ApplyConfigurationsFromAssembly`. The `IEntityTypeConfiguration` classes in `Infrastructure/Configurations/` exist purely to hold `HasData` seed rows (15 books, plus categories, members, loans).

**Migrations are applied automatically at startup** by `dbContext.Database.Migrate()` at the bottom of `LibraryApi/Program.cs`, so a new migration takes effect on the next run.

## Auth

ASP.NET Core Identity with `IdentityUser`/`IdentityRole` and **cookie** auth (`AddIdentity` + `AddDefaultTokenProviders`). `Microsoft.AspNetCore.Authentication.JwtBearer` is referenced but no JWT scheme is configured — do not assume bearer tokens work.

`[Authorize]` sits at class level on the Books, Category, Loan, and Members controllers; `AuthController` is anonymous.

`AuthController.Login` calls `SignInManager.PasswordSignInAsync` directly, bypassing MediatR. The parallel `Application/Features/Login/` slice and `IIdentityService.LoginAsync` implement the same thing and are currently unreferenced — if you touch login, pick one path rather than editing both.

## Background jobs and email

Hangfire (`Infrastructure/DependencyInjection.cs`) uses SQL Server storage on the same `DefaultConnection` and auto-creates its own schema. `AddHangfireServer()` means jobs run in-process. `app.UseHangfireDashboard()` is mapped in `LibraryApi/Program.cs` **before** `UseAuthentication()`/`UseAuthorization()`, so `/hangfire` is publicly reachable.

Email is FluentEmail + MailKit, configured from the `Email` config section (also bound to `Infrastructure/Settings/EmailSettings`).

The registration → welcome-email path **is wired** and runs through Hangfire:

`AuthController.Register` → `RegisterCommandHandler` → `_backgroundJobClient.Enqueue<SendWelcomeEmailJob>(job => job.ExecuteAsync(user.Email, user.Username))` → `SendWelcomeEmailJob` → `IEmailService` → `EmailService` (FluentEmail + MailKit).

Two things to know before touching it:

- `SendWelcomeEmailJob` **must stay registered** in `Application/DependencyInjection.cs`. Hangfire.AspNetCore's activator resolves job classes with `GetRequiredService`, so an unregistered job type throws at execution time — visible only as a failed job in `/hangfire`, never at the API call.
- `EmailService` checks `response.Successful` and throws on failure **on purpose**. FluentEmail swallows SMTP exceptions and reports them on the response, so without that check a failed send is recorded as a *succeeded* Hangfire job and never retried. Do not "simplify" it back to a bare `await ... .SendAsync()`.

Still-dead leftovers from the same refactor, safe to delete: `UserRegisteredNotification` plus its handler `Application/Features/Notifications/SendEmailHandler.cs` (nothing publishes the notification). The working tree also deletes the older `IBackgroundJobService`/`BackgroundJobService` and its DI registration in favour of Hangfire's `IBackgroundJobClient`.

## Rate limiting

Two mechanisms, neither currently limiting anything:

- The built-in fixed-window limiter named `"fixed"` is registered and `UseRateLimiter()` is called, but no endpoint carries `[EnableRateLimiting("fixed")]`.
- `LibraryApi/MiddleWares/RateLimitPerIPMiddleWare.cs` is never added to the pipeline (and its `static Dictionary` counter is not thread-safe and never resets).

## Error handling

`GlobalExceptionMiddleware` (in `LibraryApi/MiddleWares/ExceptionHandlingMiddleWare.cs` — class name and file name differ, and the class sits in the global namespace) logs and returns a generic 500 `ProblemDetails`. It is registered after the Hangfire dashboard and rate limiter, so it does not cover those.

## Deployment

`LibraryApi/Properties/PublishProfiles/site80265-WebDeploy.pubxml` publishes Release via MSDeploy to `site80265.siteasp.net` (a free ASP.NET hosting site), launching http://aram.runasp.net/ afterwards. `appsettings.Production.json` carries the matching remote SQL connection string.

## Conventions and quirks worth knowing

- Namespaces are inconsistent by layer: Domain entities are `LibraryApi.Domain.Entities` and the DbContext is `LibraryApi.Infrastructure.Data`, while Application/Infrastructure code uses bare `Application.*` / `Infrastructure.*`. One stray `LibraryApi.Application.RepositoryInterfaces` namespace exists. Match the file you are editing.
- Entities are suffixed `Model` (`BookModel`, `LoanModel`); DTOs are suffixed `DTO`.
- "Category" is pluralised as **`Categorys`** throughout (folders, interfaces, queries), and its read folder is `Query/` while every other feature uses `Queries/`. Keep the existing spelling.
- `Nullable` and `ImplicitUsings` are enabled in all four projects.
- The build emits 8 NuGet vulnerability warnings, all from transitive `MailKit`/`MimeKit` 2.10.1 via `FluentEmail.MailKit` 3.0.2. They are pre-existing, not something a change of yours introduced, and **cannot be fixed by a version override**: the patched releases (MailKit 4.16.0+, MimeKit 4.15.1+) changed `MailTransport.SendAsync`, so FluentEmail.MailKit 3.0.2 throws `MissingMethodException` at send time against them — the build stays clean, the failure is runtime-only. FluentEmail.MailKit 3.0.2 is the final release, so removing the vulnerability means replacing the sender. Note the exposure is real rather than theoretical: user-supplied registration emails reach `.To(...)`, which is the MimeKit CRLF/SMTP-injection scenario.

## Secrets

`LibraryApi/appsettings.json` and `LibraryApi/appsettings.Production.json` contain committed live-looking credentials (SQL Server passwords, a Gmail app password), and the publish profile carries the deploy username. Do not add more secrets to these files, and do not echo the existing values into logs, commits, or anything outbound.
