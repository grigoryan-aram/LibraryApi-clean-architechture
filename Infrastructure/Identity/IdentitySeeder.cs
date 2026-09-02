using Infrastructure.Settings;
using LibraryApi.Domain.Constants;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Identity
{
    /// <summary>
    /// Creates the application's roles, and the seed administrator, on startup.
    ///
    /// Roles cannot be seeded with <c>HasData</c> like the library entities:
    /// the join row between a user and a role needs the user's generated id,
    /// and the admin's password has to go through the password hasher. So this
    /// runs once per start, after the migrations, and is idempotent —
    /// everything it does is guarded by an existence check.
    /// </summary>
    public class IdentitySeeder
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly AdminAccountSettings _admin;
        private readonly ILogger<IdentitySeeder> _logger;

        public IdentitySeeder(
            RoleManager<IdentityRole> roleManager,
            UserManager<IdentityUser> userManager,
            IOptions<AdminAccountSettings> admin,
            ILogger<IdentitySeeder> logger)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _admin = admin.Value;
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            await SeedRolesAsync();
            await SeedAdminAsync();
        }

        private async Task SeedRolesAsync()
        {
            foreach (var role in Roles.All)
            {
                if (await _roleManager.RoleExistsAsync(role))
                {
                    continue;
                }

                var result = await _roleManager.CreateAsync(new IdentityRole(role));

                if (result.Succeeded)
                {
                    _logger.LogInformation("Created the {Role} role.", role);
                }
                else
                {
                    _logger.LogError(
                        "Could not create the {Role} role: {Errors}",
                        role,
                        Describe(result));
                }
            }
        }

        private async Task SeedAdminAsync()
        {
            if (!_admin.IsConfigured)
            {
                // Not an error: a developer who has not set the secrets yet
                // still gets a working site, just without a way into Swagger
                // or Hangfire. Say so rather than failing the start.
                _logger.LogWarning(
                    "No seed administrator configured (Identity:Admin:UserName, " +
                    ":Email, :Password). The Swagger UI and the Hangfire " +
                    "dashboard will be closed to everyone until one exists.");

                return;
            }

            var user = await _userManager.FindByNameAsync(_admin.UserName);

            if (user is null)
            {
                // UserName and Email map by name from AdminAccountSettings. Password is
                // deliberately not mapped — IdentityUser has no Password member, and the
                // value goes through the hasher in CreateAsync below rather than onto
                // the entity. EmailConfirmed cannot come from the map at all, since the
                // settings type has no such property, so it is set afterwards: the seed
                // administrator is trusted by definition and has no address to confirm.
                user = _admin.Adapt<IdentityUser>();
                user.EmailConfirmed = true;

                var created = await _userManager.CreateAsync(user, _admin.Password);

                if (!created.Succeeded)
                {
                    _logger.LogError(
                        "Could not create the seed administrator {UserName}: {Errors}",
                        _admin.UserName,
                        Describe(created));

                    return;
                }

                _logger.LogInformation(
                    "Created the seed administrator {UserName}.", _admin.UserName);
            }

            // Deliberately no password reset for an account that already
            // exists. Rotating the admin's password on every start would undo
            // any change made through the app, and a stale value left in
            // config would keep resetting it back.
            if (await _userManager.IsInRoleAsync(user, Roles.Admin))
            {
                return;
            }

            var added = await _userManager.AddToRoleAsync(user, Roles.Admin);

            if (added.Succeeded)
            {
                _logger.LogInformation(
                    "Granted {UserName} the {Role} role.", _admin.UserName, Roles.Admin);
            }
            else
            {
                _logger.LogError(
                    "Could not grant {UserName} the {Role} role: {Errors}",
                    _admin.UserName,
                    Roles.Admin,
                    Describe(added));
            }
        }

        private static string Describe(IdentityResult result) =>
            string.Join("; ", result.Errors.Select(error => error.Description));
    }
}
