# CLAUDE.md

Clean-architecture ASP.NET Core Web API for a library domain (books, categories, members, loans) on .NET 10, using CQRS via MediatR, `ErrorOr` for results, EF Core + SQL Server, ASP.NET Core Identity, and Hangfire for background jobs.

## Solution layout

The solution is **`LibraryApi.slnx`** at the repo root.

| Project | Path | References |
|---|---|---|
| Domain | `Domain/Domain.csproj` | — |
| Application | `Application/Application.csproj` | Domain |z
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

Test (127 tests across three projects, all passing):

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
- **Moq stores arguments by reference and re-runs `It.Is<>` matchers at `Verify` time,** so a stub that mutates what it was handed (the usual "echo it back with an id") makes an assertion about the value *on the way in* read the mutated value. `AddMemberCommandHandlerTests` records the id inside the stub instead. Looks like a handler bug; is not one.

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

Books, categories and members each have **Add / Update / Delete / GetAll / GetById**; loans have no general update (see **Lending**). The `PUT /api/{resource}/{id}` actions take the command from the body and **overwrite its `Id` from the route** with `command with { Id = id }`, so the two cannot disagree. Update handlers load the entity, assign only what the command carries, and write it back — never `Adapt` onto a fresh entity, which would blank the rest.

Every repository read is `AsNoTracking()`, so `UpdateAsync` / `UpdateCategoryAsync` / `UpdateMemberAsync` / `UpdateLoanAsync` must call `Update()` to attach the detached instance. A bare `SaveChangesAsync` saves nothing.

## Lending

The loan slice is the one place in this codebase with real domain rules, so it does not look quite like the CRUD slices around it.

`POST /api/Loans` takes **`{ bookId, memberId }`** and nothing else. `BorrowedAt` and `DueAt` are stamped by `AddLoanCommandHandler` from the server clock — a caller who can choose their own borrow date can choose their own due date with it. The handler checks both ids against `IBooksRepository`/`IMembersRepository` first: without that, an unknown id reached SQL Server and came back as a foreign-key violation, which `GlobalExceptionMiddleware` turned into a **500** for what is plainly a bad request. It is now `Loans.BookNotFound` / `Loans.MemberNotFound`. `AddBookCommandHandler` and `UpdateBookCommandHandler` check `CategoryId` the same way, for the same reason, and answer `Books.CategoryNotFound`.

`POST /api/Loans/{id}/return` (`ReturnLoanCommand`) is the only way a loan record changes. It refuses a second return with `Loans.AlreadyReturned` rather than moving `ReturnedAt` forward and quietly rewriting when the book came back. There is deliberately no general `UpdateLoanCommand`: an endpoint that could rewrite `BorrowedAt` or `DueAt` would be an endpoint for corrupting the record. Note `DELETE /api/Loans/{id}` still exists and is *not* returning a book — it erases the fact that the loan happened.

`GET /api/Loans/overdue` (`GetOverdueLoansQuery`) filters in SQL (`ReturnedAt IS NULL AND DueAt < @asOf`) rather than pulling every loan back and sifting it in memory, and returns an **empty list** rather than `NotFound` — nothing overdue is a good answer, not a missing one.

`GET /api/Loans/mine` (`GetMyLoansQuery`) returns the caller's own loans. **`IdentityUserId` is never model-bound** — `LoansController` reads `ClaimTypes.NameIdentifier` off the auth cookie, same as `AskClaudeQuery.Requester`; from the query string it would read a stranger's history. It is the Identity user id, **not** `User.Identity.Name`. Mapped **before** `{id}`, like `overdue`. An account with no member row gets `Loans.NoMemberForAccount`, not an empty list.

The loan period is **`Loans:LoanPeriodDays`, default 14**, reached through `ILoanPolicy` (`Application/ServiceInterfaces/`) with `ConfiguredLoanPolicy` behind it. The interface exists because the handler lives in Application and configuration is an Infrastructure concern — the same shape as `IAiUsageLimiter`. A configured period of 0 or less is treated as a typo and falls back to 14, because honouring it would make every loan overdue the instant it was created.

Things that will bite:

- **`DueAt` is stored, not computed on read.** Changing `LoanPeriodDays` affects only loans handed out afterwards; it cannot retroactively make yesterday's loans overdue. The `AddLoanDueDate` migration backfills existing rows with `BorrowedAt + 14 days` — the column default of `0001-01-01` would otherwise have made every loan already on the books permanently overdue. Rows whose `BorrowedAt` is itself `0001-01-01` are left alone: they predate the fix that stopped `AddLoanAsync` dropping the borrow date, and there is no honest due date to infer for them.
- **`IsOverdue`, `IsReturned` and `DaysOverdue` are computed getters on `LoansDTO`,** not columns and not mapped by Mapster (it fills constructor parameters and leaves get-only members alone). Both the API and `Loans.razor` read them, so the page and `/overdue` cannot disagree.
- **`DaysOverdue` rounds DOWN.** A book three days and one millisecond late is three days overdue; rounding up called it four. The cost is that anything late by less than a day reports `0`, so read it together with `IsOverdue` — never on its own. `Loans.razor` has a branch for exactly that case and shows a bare "overdue" badge.
- **Availability is `TotalCopies` minus the open loans, counted in SQL — never a stored flag.** `BookModel.IsBorrowed` was a dead boolean nothing maintained, which is why one book could go out to several members. `AddLoanCommandHandler` now refuses with `Loans.NoCopiesAvailable`. `CK_Books_TotalCopies_Positive` enforces `>= 1`.

- **Two counting methods, on purpose:** `CountActiveLoansForBookAsync` for the lending path and `GetBookByIdQuery`; `CountActiveLoansByBookAsync` (one `GROUP BY`) for `GetAllBooksQuery`, or listing the catalogue would be N+1. Books with nothing out are **absent** from that dictionary, so a missing key means 0.

- **`CopiesOnLoan` is not mapped** — it has no counterpart on `BookModel`, so Mapster leaves it 0 and each read handler supplies it with `with { CopiesOnLoan = n }`. Forget it and the response claims every copy is free. `AvailableCopies` and `IsAvailable` are computed getters; `AvailableCopies` clamps at 0.

- **`UpdateBookCommand` refuses to cut `TotalCopies` below what is on loan** (`Books.CopiesBelowActiveLoans`), or the clamp above would swallow the shortfall.

- **Two callers can still race past the availability check** — the count and the insert are separate statements with no lock between them. Not prevented; fixing it needs a transaction with the right isolation level, or a row per copy.
- **Timestamps come back from the database with no `DateTimeKind`.** The values are UTC, and every server-side comparison is therefore correct, but `POST` echoes `…Z` while `GET` returns the same instant without it. A client that parses the unsuffixed form as local time will compute overdue wrongly. Storing `DateTimeOffset`, or stamping `DateTimeKind.Utc` on read, is the fix.
- **`AddMemberCommand` takes only a name.** It used to require a client-supplied `id` greater than 0, so creating a member meant choosing your own primary key; the identity column assigns it now, and the handler builds the entity by hand rather than adapting the command precisely so `Id` stays 0. Members are still not seeded (see `MembersConfiguration`, which explains why a `HasData` block there would break startup on any database that already holds members) — but **registration now creates one**, so a fresh database gets its first member as soon as somebody signs up.

## Data access

Repository-per-aggregate. Interfaces in `Application/RepositoryInterfaces/`, EF Core implementations in `Infrastructure/Repositories/`. Conventions in the existing repos: reads use `AsNoTracking()`, deletes use `ExecuteDeleteAsync()` (no load-then-remove, so deleting a missing id is a silent no-op), every method takes a `CancellationToken`.

`LibraryDBContext` (`Infrastructure/Data/LibraryDBContext.cs`) extends `IdentityDbContext<IdentityUser>`, declares relationships inline in `OnModelCreating`, then calls `ApplyConfigurationsFromAssembly`. The `IEntityTypeConfiguration` classes in `Infrastructure/Configurations/` mostly hold `HasData` seed rows (15 books — three of them with more than one copy, so availability is something other than a rephrased boolean out of the box — plus categories); `MembersConfiguration` holds no seed at all and exists for the unique index, and `BooksConfiguration` also carries the `TotalCopies >= 1` check constraint.

**A non-nullable column added by a migration needs its backfill written by hand.** EF scaffolds `defaultValue: 0`; `AddCopiesAndMemberAccounts` had to be edited to `1`, or every book added through the API (outside the seed, so `UpdateData` misses it) would be unlendable *and* the check constraint later in the same migration would fail inside `Database.Migrate()`. `AddLoanDueDate` is the same lesson about `DueAt`.

**Migrations are applied automatically at startup** by `dbContext.Database.Migrate()` at the bottom of `LibraryApi/Program.cs`, so a new migration takes effect on the next run.

## Auth

ASP.NET Core Identity with `IdentityUser`/`IdentityRole` and **cookie** auth (`AddIdentity` + `AddDefaultTokenProviders`). `Microsoft.AspNetCore.Authentication.JwtBearer` is referenced but no JWT scheme is configured — do not assume bearer tokens work.

`[Authorize]` sits at class level on the Books, Category, Loan, and Members controllers; `AuthController` is anonymous.

`AuthController.Login` calls `SignInManager.PasswordSignInAsync` directly, bypassing MediatR. The parallel `Application/Features/Login/` slice and `IIdentityService.LoginAsync` implement the same thing and are currently unreferenced — if you touch login, pick one path rather than editing both.

### Members and accounts

An Identity account and a library member are separate, joined by **`MemberModel.IdentityUserId`** with a **filtered unique index** (`WHERE [IdentityUserId] IS NOT NULL`). Nullable both ways: walk-ins added via `POST /api/Members` have no account, and accounts predating the column have no member. Filtered because null is the normal case — a plain unique index would allow only one walk-in.

**`RegisterCommandHandler` creates the pair**, which is what makes `/api/Loans/mine` answerable. `RegisteredUserDTO` carries `UserId` only for that; `AuthController` never serialises the DTO, so it stays off the wire. Three things:

- It is **idempotent** — it looks the member up by `IdentityUserId` first, because the unique index would reject a second row.
- **Failures are logged, not returned.** The account exists by then, so an error would tell the caller registration failed on a username they now own. The welcome email must still be queued when the member insert fails — there is a test for it.
- The member is named after the **username**, not the email.

**`UpdateMemberCommand` cannot touch `IdentityUserId`.** It loads the entity, sets only `Name`, and writes it back, so a rename cannot silently unlink an account and orphan that person's loan history.

### Roles

Two roles, named once in `Domain/Constants/Roles.cs`: **`Admin`** and **`User`**. They exist to keep the **Swagger UI and the Hangfire dashboard** — the two surfaces that expose every endpoint and every job argument — away from ordinary accounts. Nothing else checks a role; the resource controllers still ask only for `[Authorize]`.

- **`User` is granted by `IdentityService.RegisterAsync`** to every account it creates. There is deliberately no path from the public registration endpoint to `Admin`.
- **`Admin` is granted only by `IdentitySeeder`** (`Infrastructure/Identity/`), which runs from `Program.cs` in the same startup scope as `Database.Migrate()`, right after it. It creates any missing role, then creates the account named by `Identity:Admin` and puts it in `Admin`. Everything it does is guarded by an existence check, so it is safe on every start.
- **The seed admin's credentials are never in `appsettings*.json`** — `Identity:Admin:UserName`, `:Email` and `:Password` ship empty, exactly like the other secrets. Set them with `dotnet user-secrets set "Identity:Admin:Password" "…"` from `LibraryApi/`, or in `appsettings.Secrets.json` on the host. With any of the three missing the seeder logs a warning and creates no administrator — a default password in config would be a back door on every deployment.
- **An existing account is promoted, never re-passworded.** If `Identity:Admin:UserName` names a user who already exists, the seeder adds the role and leaves the password alone; rotating it on every start would undo any change made through the app.

Both gates ask `AdminAccess.IsAdmin` (`LibraryApi/Extensions/AdminAccess.cs`) so they cannot drift apart:

- **Hangfire** — `HangfireDashboardAuthorization` returns false for anyone without the role. Hangfire's dashboard has no notion of a redirect: anonymous gets **401**, a signed-in non-admin gets **403** (both measured).
- **Swagger** — `AdminOnlyPathMiddleWare` (`LibraryApi/MiddleWares/`) gates the `/swagger` prefix, mapped in `Program.cs` immediately before `UseSwagger()`. Swagger is served by middleware, not an endpoint, so there is no route to hang `[Authorize(Roles = "Admin")]` on; gating the path is the only way. It **challenges** an anonymous caller and **forbids** a signed-in non-admin, because signing in again would change nothing. The prefix match also covers `/swagger/v1/swagger.json` — gating only the UI page would leave the document readable.

What that actually looks like on the wire is **302 to `/account` in both cases**, which is worth knowing before you go hunting for a bug:

| caller | `/swagger/*` | `/hangfire` | an `[Authorize]` API route |
|---|---|---|---|
| anonymous | 302 → `/account?ReturnUrl=…` | 401 | 401 (with a `Location` header) |
| signed in, no `Admin` | 302 → `/account?ReturnUrl=…` | 403 | 200 |
| signed in, `Admin` | 200 | 200 | 200 |

Two things drive that and neither is a defect: `ConfigureApplicationCookie` points **both** `LoginPath` and `AccessDeniedPath` at `/account`, so a challenge and a forbid land on the same URL; and the cookie handler only downgrades a redirect to a 401 for requests carrying `X-Requested-With: XMLHttpRequest` — an `Accept: application/json` header does **not** do it. The API routes answer 401 rather than redirecting because they are matched endpoints with API metadata, while `/swagger` is middleware with no endpoint at all. A signed-in non-admin being redirected to `/account` — a page that will tell them they are already signed in — is a genuine dead end; a dedicated access-denied page would be the fix.

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
- `EmailService` checks `response.Successful` **on purpose** and returns `Error.Failure("Email.SendFailed", …)`. FluentEmail swallows SMTP exceptions and reports them on the response, so without that check a failed send is recorded as a *succeeded* Hangfire job and never retried. Do not "simplify" it back to a bare `await ... .SendAsync()`.
- **`SendWelcomeEmailJob` is the one place that throws on purpose**, and it is not control flow — it is the only vocabulary Hangfire has. Hangfire decides a job's fate by whether the method threw: a job that *returns* is recorded as **Succeeded**, and a returned `ErrorOr` is still a return. So `IEmailService` returns `ErrorOr<Success>` and the job translates a failure into the signal the scheduler understands. Measured: a failed send lands in **Scheduled** state with the error description as the retry reason (`Retry attempt 2 of 10: Failed to send welcome email to …`) — note that a first failure appears in the *retry* list, not the failed list.

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

**Never hand-throw an exception.** Failure is modelled with the result pattern via `ErrorOr`: return `ErrorOr<T>` and a specific `Error` (`Loans.BookNotFound`, not a bare failure) rather than throwing. A thrown exception bypasses `ValidationBehavior<,>`, the handler's error contract and `ToProblem`, and lands in `GlobalExceptionMiddleware` as an opaque 500 — the wrong answer for anything the caller could correct. When a third-party library throws (the Anthropic SDK, FluentEmail), catch at that boundary and map to an `Error`, the way `ClaudeService` maps the SDK exception chain to `Claude.*` codes.

Two deliberate consequences of that rule:

- **`AddInfrastructure` returns `ErrorOr<Success>`, not `IServiceCollection`.** A missing connection string or a non-numeric `Email:Smtp:Port` is reported, not thrown; `Program.cs` prints `Cannot start: [code] description`, sets exit code 1 and returns **before** `builder.Build()`, so nothing has opened a database connection or started a Hangfire worker. Nothing chained off the old return value.
- **`SendWelcomeEmailJob` is the single exception**, for the Hangfire reason described under **Background jobs and email**.

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
