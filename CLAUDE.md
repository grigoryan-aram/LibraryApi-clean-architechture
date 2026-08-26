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
| Application tests | `tests/Application.UnitTests/` | Application |
| Infrastructure tests | `tests/Infrastructure.UnitTests/` | Infrastructure |
| Presentation tests | `tests/Presentation.UnitTests/` | Presentation |

Presentation references only Infrastructure; Application/Domain come in transitively. Keep the inward dependency flow: Domain has no dependencies, Application depends only on Domain plus abstractions it declares itself, Infrastructure implements those abstractions.

Note the mismatch between folder and project name: the web host lives in `LibraryApi/` but its project is `Presentation.csproj` (assembly `Presentation`, `RootNamespace` `LibraryApi`).

### Leftovers from a recent cleanup

The repo used to carry a duplicate set of host files at the root (`Program.cs`, `Presentation.csproj`, `appsettings*.json`, `dotnet-tools.json`, `Library API.http`) plus a second solution file at `LibraryApi/LibraryApi.slnx`. Those deletions and the root `LibraryApi.slnx` rewrite that replaced them are now committed. The live host files are all under `LibraryApi/` — if you ever see a `Program.cs` at the repo root again, it came back from an old branch or stash and is not the one that builds.

Still orphaned, and reasonable to delete: root `Properties/launchSettings.json` (committed, but it declares a `Presentation` profile on ports 50522/50523 with no project at the root to own it) and the stale root `bin/`/`obj/`. The live launch profiles are `LibraryApi/Properties/launchSettings.json`.

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

Test (58 tests across three projects, all passing):

```bash
dotnet test LibraryApi.slnx
```

`dotnet build` and `dotnet test` are the only automated checks — there is no CI workflow. See **Keeping tests current** below before changing code.

## Keeping tests current

Every code change here includes bringing the unit tests with it — add tests for new behavior, update the ones the change invalidated, drop the ones whose code is gone, then run `dotnet test` and report the real result. This is part of the change, not a follow-up.

The stack is **xUnit + Moq with xUnit's built-in `Assert`** — deliberately no assertion library, partly to keep dependencies down and partly because FluentAssertions v8+ requires a paid licence for commercial use. Test files mirror the source path (`Application/Features/Registration/…` → `tests/Application.UnitTests/Features/Registration/…`). Add a test project per production project you actually test, named `<Project>.UnitTests`, rather than folding Infrastructure tests into `Application.UnitTests`.

Highest-value targets, given this codebase's shape: MediatR handlers (mock the repository or service interface, assert the `ErrorOr` error *type and code*, not merely that it errored), the FluentValidation validators, and the convention-based Mapster mappings — those break silently on a property rename, which is the most likely way to break this app without a compiler error.

Traps that have already cost time here:

- **`ExecuteDeleteAsync` throws on the EF Core InMemory provider.** Every repository in this codebase uses it for deletes, and it is relational-only. Repository tests need the SQLite in-memory provider (`Microsoft.EntityFrameworkCore.Sqlite` with a `DataSource=:memory:` connection held open for the test's lifetime). The InMemory provider also enforces no constraints at all, so it cannot prove anything about foreign keys or uniqueness.
- **Match the exact overload when mocking a fluent builder.** `IFluentEmail` declares `To(string)`, `To(string, string)` and `To(IEnumerable<Address>)`; `EmailService` calls the single-argument one. Setting up a sibling overload leaves the real call returning Moq's default `null`, which surfaces as a `NullReferenceException` from inside the chain rather than as an obvious mock-setup mistake.
- **`Enqueue<T>` is an extension method,** so Moq cannot see it. Assert Hangfire enqueues through `IBackgroundJobClient.Create(Job, IState)` instead, and check `Job.Args` — that is what catches an argument-order swap.
- **Prefer asserting the returned result over `Verify`.** Reach for `Verify` when the call itself *is* the behavior (enqueuing a job, sending an email); call-count assertions on anything else break under harmless refactors.

Run the suite before reporting a change done, and report what it actually printed. A test that was never executed is a claim, not a check — and if you fix something that used to swallow a failure, the test proving it now surfaces that failure is the one most worth writing.

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

### Roles

Two roles, named once in `Domain/Constants/Roles.cs`: **`Admin`** and **`User`**. They exist to keep the **Swagger UI and the Hangfire dashboard** — the two surfaces that expose every endpoint and every job argument — away from ordinary accounts. Nothing else checks a role; the resource controllers still ask only for `[Authorize]`.

- **`User` is granted by `IdentityService.RegisterAsync`** to every account it creates. There is deliberately no path from the public registration endpoint to `Admin`.
- **`Admin` is granted only by `IdentitySeeder`** (`Infrastructure/Identity/`), which runs from `Program.cs` in the same startup scope as `Database.Migrate()`, right after it. It creates any missing role, then creates the account named by `Identity:Admin` and puts it in `Admin`. Everything it does is guarded by an existence check, so it is safe on every start.
- **The seed admin's credentials are never in `appsettings*.json`** — `Identity:Admin:UserName`, `:Email` and `:Password` ship empty, exactly like the other secrets. Set them with `dotnet user-secrets set "Identity:Admin:Password" "…"` from `LibraryApi/`, or in `appsettings.Secrets.json` on the host. With any of the three missing the seeder logs a warning and creates no administrator — a default password in config would be a back door on every deployment.
- **An existing account is promoted, never re-passworded.** If `Identity:Admin:UserName` names a user who already exists, the seeder adds the role and leaves the password alone; rotating it on every start would undo any change made through the app.

Both gates ask `AdminAccess.IsAdmin` (`LibraryApi/Extensions/AdminAccess.cs`) so they cannot drift apart:

- **Hangfire** — `HangfireDashboardAuthorization` returns false for anyone without the role. Hangfire's dashboard has no notion of a redirect, so that is a flat 401 for anonymous and non-admin alike.
- **Swagger** — `AdminOnlyPathMiddleWare` (`LibraryApi/MiddleWares/`) gates the `/swagger` prefix, mapped in `Program.cs` immediately before `UseSwagger()`. Swagger is served by middleware, not an endpoint, so there is no route to hang `[Authorize(Roles = "Admin")]` on; gating the path is the only way. It **challenges** an anonymous caller (the cookie handler turns that into a redirect to `/account` for a browser, a 401 for anything else) and **forbids** a signed-in non-admin, because signing in again would change nothing. The prefix match also covers `/swagger/v1/swagger.json` — gating only the UI page would leave the document readable.

Two things that will look like bugs and are not:

- **Role claims live in the auth cookie**, minted at sign-in. A user who was signed in when their role changed keeps the old answer until the cookie is refreshed or they sign in again. After seeding a new admin, sign out and back in.
- The Swagger and Hangfire links in `MainLayout.razor` are inside `<AuthorizeView Roles="@Roles.Admin">`, so they disappear for everyone else. That is cosmetic only — the two gates above are the actual enforcement.

## Background jobs and email

Hangfire (`Infrastructure/DependencyInjection.cs`) uses SQL Server storage on the same `DefaultConnection` and auto-creates its own schema. `AddHangfireServer()` means jobs run in-process. `app.UseHangfireDashboard("/hangfire", …)` is mapped in `LibraryApi/Program.cs` **after** `UseAuthentication()`/`UseAuthorization()` and carries a `HangfireDashboardAuthorization` filter (`LibraryApi/Extensions/`) that requires the **`Admin`** role — see **Roles** above; everyone else gets a 401. It used to sit before the auth middleware with no filter, which left the dashboard, and every job argument in it, open to anyone with the URL. Keep it where it is.

Email is FluentEmail + MailKit, configured from the `Email` config section (also bound to `Infrastructure/Settings/EmailSettings`).

The registration → welcome-email path **is wired** and runs through Hangfire:

`AuthController.Register` → `RegisterCommandHandler` → `_backgroundJobClient.Enqueue<SendWelcomeEmailJob>(job => job.ExecuteAsync(user.Email, user.Username))` → `SendWelcomeEmailJob` → `IEmailService` → `EmailService` (FluentEmail + MailKit).

Two things to know before touching it:

- `SendWelcomeEmailJob` **must stay registered** in `Application/DependencyInjection.cs`. Hangfire.AspNetCore's activator resolves job classes with `GetRequiredService`, so an unregistered job type throws at execution time — visible only as a failed job in `/hangfire`, never at the API call.
- `EmailService` checks `response.Successful` and throws on failure **on purpose**. FluentEmail swallows SMTP exceptions and reports them on the response, so without that check a failed send is recorded as a *succeeded* Hangfire job and never retried. Do not "simplify" it back to a bare `await ... .SendAsync()`.

`RegisterCommandHandler` and `EmailService` both have unit tests covering exactly these two behaviors — see `tests/`.

Still-dead leftovers from the same refactor, safe to delete: `UserRegisteredNotification` plus its handler `Application/Features/Notifications/SendEmailHandler.cs` (nothing publishes the notification). The older `IBackgroundJobService`/`BackgroundJobService` and its DI registration were removed in favour of Hangfire's `IBackgroundJobClient`; that deletion is committed.

## Claude AI chat endpoint

`GET /api/ClaudeAI?message=…&conversationId=…` is a chatbot usable straight from Swagger. It follows the normal slice shape — `ClaudeAIController` → `AskClaudeQuery` → `AskClaudeQueryHandler` → `IClaudeService` → `ClaudeService` (official `Anthropic` NuGet SDK, `client.Messages.Create`) — and is `[Authorize]` like the other resource controllers, so log in via `/api/Auth/login` first and Swagger's cookie carries over. It is a `Query` rather than a `Command` because it is a GET, matching the rest of the codebase.

**This slice never touches `LibraryDBContext`.** Leave `conversationId` empty to start a chat; the reply carries the id to pass back on the next call. History lives in `IChatHistoryStore` / `InMemoryChatHistoryStore` (an `IMemoryCache` entry with a 30-minute sliding expiry) — process-local and lost on restart, which is fine for Swagger sessions but is *not* a durable store. The only database contact on such a request comes from the Identity cookie behind `[Authorize]`, not from the slice itself.

Things that will bite:

- **The API key is never in source control.** `appsettings.json` ships `Claude:ApiKey` empty; `Infrastructure/DependencyInjection.cs` falls back to the `ANTHROPIC_API_KEY` environment variable. Set it with `dotnet user-secrets set "Claude:ApiKey" "sk-ant-…"` from `LibraryApi/` (run `dotnet user-secrets init` once) or export the env var. With neither, the endpoint returns a `Claude.ApiKeyMissing` failure — it does not throw.
- **`ClaudeService` is a singleton holding a `Lazy<AnthropicClient>`.** One `HttpClient` for the app's lifetime, and the client is only constructed once a key is present, which is what keeps the missing-key path an `ErrorOr` instead of a container-resolution exception on every request.
- **Every stored turn is resent on the next call, so history is capped** at 20 messages in the handler and the message itself at 4000 characters in the validator. Both caps exist to bound cost; removing them is a billing decision.
- **The call is non-streaming on purpose** — Swagger UI cannot render an SSE stream. Model, `MaxTokens` and the system prompt come from the `Claude` config section; `Effort.Medium` is set in code to keep the synchronous request from hanging Swagger.
- **Errors are mapped, not thrown.** `ClaudeService` catches the SDK exception chain (unauthorized → rate limited → 5xx → I/O → base) and returns `Error.Failure` codes prefixed `Claude.*`, which `ToProblem` surfaces as a 400.

## Blazor front end

The host also serves a small Blazor Server UI from `LibraryApi/Components/` — `App.razor` (root document), `Routes.razor`, `Layout/MainLayout.razor`, and three pages: `Pages/Home.razor` (`/`), `Pages/Chat.razor` (`/chat`), `Pages/Account.razor` (`/account`). Styling is one hand-written `wwwroot/app.css`; `wwwroot/app.js` holds a single scroll helper. There is no client project and no JS build step.

Wiring in `Program.cs`: `AddRazorComponents().AddInteractiveServerComponents()`, `AddCascadingAuthenticationState()`, `AddHttpContextAccessor()`, then `UseAntiforgery()` (after auth, before the endpoints), `MapStaticAssets()` and `MapRazorComponents<App>().AddInteractiveServerRenderMode()`.

Four things here will waste your time if you do not know them:

- **`@using Application.X` does not compile in a `.razor` file.** `RootNamespace` is `LibraryApi`, so Razor resolves it as `LibraryApi.Application.X`. `Components/_Imports.razor` pins them with `@using global::Application.DTOs` etc. Add new Application usings there, with `global::`.
- **`Chat.razor` is interactive; `Account.razor` deliberately is not.** Signing in writes an auth cookie, and an interactive circuit has no response left to write headers on. The login/register/sign-out forms are static-SSR posts (`EditForm` + `FormName` + `[SupplyParameterFromForm]`), which is the same shape the ASP.NET Identity templates use. Do not add `@rendermode` to that page.
- **`AuthorizeView` and `EditForm` both bind `context`.** Nesting a form inside `<AuthorizeView>` is a compile error until you name one — `Account.razor` uses `<AuthorizeView Context="auth">`.
- **`ConfigureApplicationCookie` points `LoginPath` at `/account`.** Identity defaults to `/Account/Login`, which does not exist here, so a browser hitting `/chat` signed-out was being redirected to a 404. API callers are unaffected: the cookie handler still answers 401 when the request does not accept HTML, which is why `GET /api/ClaudeAI` returns 401 to curl but 302 to a browser.

`Chat.razor` injects `IMediator` and sends `AskClaudeQuery` **in-process** — component → handler, no HTTP hop back into this same app. The interactive circuit is a WebSocket (SignalR); on hosting without WebSocket support SignalR silently falls back to long polling, which still works but is worse. The components have no unit tests — bUnit would be a new dependency for markup that holds no logic; they were verified by driving a real browser instead.

## Rate limiting

Three mechanisms. Only the third actually limits anything:

- The built-in fixed-window limiter named `"fixed"` is registered and `UseRateLimiter()` is called, but no endpoint carries `[EnableRateLimiting("fixed")]`.
- `LibraryApi/MiddleWares/RateLimitPerIPMiddleWare.cs` is never added to the pipeline (and its `static Dictionary` counter is not thread-safe and never resets).
- **`IAiUsageLimiter` caps Claude at one message per user per 24 hours** (`Claude:RateLimitHours`, 0 disables). `InMemoryAiUsageLimiter` keys an `IMemoryCache` entry on the requester with an absolute expiry.

That last one deliberately sits **in `AskClaudeQueryHandler`, not on the endpoint**. ASP.NET Core's rate limiter only sees HTTP requests, and the Blazor chat page calls `IMediator` in-process — an `[EnableRateLimiting]` attribute would have limited curl while leaving the UI unlimited. Putting it behind the handler covers both entry points, which is the whole point.

Two details worth keeping:

- **The allowance is checked before the call and recorded only after Claude answers.** A failed turn (missing key, rate-limited upstream, refusal) costs the caller nothing — otherwise an unset API key would lock every user out for 24 hours over a failure that was never theirs.
- **`AskClaudeQuery.Requester` is never model-bound.** The controller takes it from `User.Identity.Name` and `Chat.razor` from its cascading `AuthenticationState`; a caller cannot put it in the query string and spend someone else's allowance. The validator rejects an empty one, which would otherwise mean one shared allowance for everybody.

Being in-memory, the allowance resets on restart and is per-instance. Fine for one small deployment; move it to the database if it ever has to be authoritative.

## Error handling

`GlobalExceptionMiddleware` (in `LibraryApi/MiddleWares/ExceptionHandlingMiddleWare.cs` — class name and file name differ, and the class sits in the global namespace) logs and returns a generic 500 `ProblemDetails`. It is registered after the Hangfire dashboard and rate limiter, so it does not cover those.

## Transport security

One switch controls all of it: **`Security:RequireHttps`** (read in `Program.cs`, defaults to `true`). When true, `UseHttpsRedirection()` runs first in the pipeline, `UseHsts()` is added outside Development, and the Identity cookie's `SecurePolicy` is `Always` outside Development. When false, none of those apply and the cookie is `SameAsRequest`. The cookie is `HttpOnly` and `SameSite=Lax` either way.

**It is `false` in `appsettings.Production.json`, deliberately.** MonsterASP's free plan does not offer a TLS certificate — Let's Encrypt is Premium-only there, and `runasp.net` is not on the Public Suffix List, so a self-obtained Let's Encrypt certificate would contend for the rate limit of a domain shared with every other free customer. The site is therefore served over plain HTTP, which means **passwords, the session cookie and the Blazor WebSocket are not encrypted in transit**. The app logs a warning on every start while this is the case. Set it back to `true` the day a certificate exists; that restores the redirect, HSTS and the `Secure` flag together.

Do not "fix" this by setting `Secure` while the host is HTTP-only: the browser accepts such a cookie and never sends it back, so sign-in fails with nothing in the logs to explain it. `UseHsts()` also emits nothing on `localhost` by design, so that header can only ever be confirmed on the real domain.

## Deployment

`LibraryApi/Properties/PublishProfiles/site80265-WebDeploy.pubxml` publishes Release via MSDeploy to `site80265.siteasp.net` (a free ASP.NET hosting site), launching http://aram.runasp.net/ afterwards. `appsettings.Production.json` carries the matching remote SQL connection string.

## Conventions and quirks worth knowing

- Namespaces are inconsistent by layer: Domain entities are `LibraryApi.Domain.Entities` and the DbContext is `LibraryApi.Infrastructure.Data`, while Application/Infrastructure code uses bare `Application.*` / `Infrastructure.*`. One stray `LibraryApi.Application.RepositoryInterfaces` namespace exists. Match the file you are editing.
- Entities are suffixed `Model` (`BookModel`, `LoanModel`); DTOs are suffixed `DTO`.
- "Category" is pluralised as **`Categorys`** throughout (folders, interfaces, queries), and its read folder is `Query/` while every other feature uses `Queries/`. Keep the existing spelling.
- `Nullable` and `ImplicitUsings` are enabled in all six projects, test projects included.
- The build emits 12 NuGet vulnerability warnings, all from transitive `MailKit`/`MimeKit` 2.10.1 via `FluentEmail.MailKit` 3.0.2 (two advisories, repeated per project that pulls them in — Infrastructure, Presentation, and now `Infrastructure.UnitTests`). They are pre-existing, not something a change of yours introduced, and **cannot be fixed by a version override**: the patched releases (MailKit 4.16.0+, MimeKit 4.15.1+) changed `MailTransport.SendAsync`, so FluentEmail.MailKit 3.0.2 throws `MissingMethodException` at send time against them — the build stays clean, the failure is runtime-only. FluentEmail.MailKit 3.0.2 is the final release, so removing the vulnerability means replacing the sender. Note the exposure is real rather than theoretical: user-supplied registration emails reach `.To(...)`, which is the MimeKit CRLF/SMTP-injection scenario.

## Secrets

**Nothing secret lives in `appsettings*.json` any more.** `ConnectionStrings:DefaultConnection`, `Email:User`, `Email:Password`, `Claude:ApiKey` and `Identity:Admin:*` are all empty strings in both files. Non-secret settings (`Email:From`, `Email:Smtp:*`, the `Claude` model/limits) stay in config.

Local development reads them from **user secrets** (`UserSecretsId` is on `Presentation.csproj`; the values are already set on this machine):

```bash
dotnet user-secrets list
```

```bash
dotnet user-secrets set "Claude:ApiKey" "sk-ant-..."
```

Production reads them from **environment variables** on the host — double underscore for nesting:

```
ConnectionStrings__DefaultConnection
Email__User
Email__Password
Claude__ApiKey        (or ANTHROPIC_API_KEY, which Infrastructure/DependencyInjection.cs falls back to)
Identity__Admin__UserName
Identity__Admin__Email
Identity__Admin__Password
```

`AddInfrastructure` throws at startup with a pointed message if the connection string is missing, rather than failing later inside EF.

**The old values are still in git history** (`git log -p -- LibraryApi/appsettings.json`), so scrubbing the files did not un-leak them — the SQL password and the Gmail app password both need rotating. The publish profile still carries the deploy username. Do not add new secrets to these files, and do not echo the existing values into logs, commits, or anything outbound.
