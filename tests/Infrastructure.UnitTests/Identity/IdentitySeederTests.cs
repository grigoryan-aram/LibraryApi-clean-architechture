using Infrastructure.Identity;
using Infrastructure.Settings;
using LibraryApi.Domain.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Infrastructure.UnitTests.Identity;

public class IdentitySeederTests
{
    private readonly Mock<RoleManager<IdentityRole>> _roleManager = MockRoleManager();
    private readonly Mock<UserManager<IdentityUser>> _userManager = MockUserManager();

    private static readonly AdminAccountSettings ConfiguredAdmin = new()
    {
        UserName = "ada",
        Email = "ada@example.com",
        Password = "Sup3r-secret!"
    };

    // Moq has to call the real constructors, so every argument has to be
    // there — the counts are what they are in ASP.NET Core Identity.
    private static Mock<RoleManager<IdentityRole>> MockRoleManager() =>
        new(Mock.Of<IRoleStore<IdentityRole>>(), null!, null!, null!, null!);

    private static Mock<UserManager<IdentityUser>> MockUserManager() =>
        new(Mock.Of<IUserStore<IdentityUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);

    private IdentitySeeder CreateSut(AdminAccountSettings? admin = null) =>
        new(_roleManager.Object,
            _userManager.Object,
            Options.Create(admin ?? new AdminAccountSettings()),
            NullLogger<IdentitySeeder>.Instance);

    private void GivenRolesExist(bool exist) =>
        _roleManager.Setup(manager => manager.RoleExistsAsync(It.IsAny<string>()))
                    .ReturnsAsync(exist);

    private void GivenRoleCreationSucceeds() =>
        _roleManager.Setup(manager => manager.CreateAsync(It.IsAny<IdentityRole>()))
                    .ReturnsAsync(IdentityResult.Success);

    private void GivenAdminUser(IdentityUser? user) =>
        _userManager.Setup(manager => manager.FindByNameAsync(ConfiguredAdmin.UserName))
                    .ReturnsAsync(user);

    [Fact]
    public async Task Creates_every_role_that_is_missing()
    {
        GivenRolesExist(false);
        GivenRoleCreationSucceeds();

        await CreateSut().SeedAsync();

        foreach (var role in Roles.All)
        {
            _roleManager.Verify(
                manager => manager.CreateAsync(It.Is<IdentityRole>(r => r.Name == role)),
                Times.Once);
        }
    }

    // Seeding runs on every start, so it has to be a no-op on the second one.
    [Fact]
    public async Task Creates_nothing_when_the_roles_are_already_there()
    {
        GivenRolesExist(true);

        await CreateSut().SeedAsync();

        _roleManager.Verify(
            manager => manager.CreateAsync(It.IsAny<IdentityRole>()), Times.Never);
    }

    [Fact]
    public async Task Creates_the_seed_administrator_and_grants_the_admin_role()
    {
        GivenRolesExist(true);
        GivenAdminUser(null);
        _userManager.Setup(manager => manager.CreateAsync(
                        It.IsAny<IdentityUser>(), ConfiguredAdmin.Password))
                    .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(manager => manager.IsInRoleAsync(
                        It.IsAny<IdentityUser>(), Roles.Admin))
                    .ReturnsAsync(false);
        _userManager.Setup(manager => manager.AddToRoleAsync(
                        It.IsAny<IdentityUser>(), Roles.Admin))
                    .ReturnsAsync(IdentityResult.Success);

        await CreateSut(ConfiguredAdmin).SeedAsync();

        _userManager.Verify(manager => manager.CreateAsync(
            It.Is<IdentityUser>(user =>
                user.UserName == ConfiguredAdmin.UserName
                && user.Email == ConfiguredAdmin.Email),
            ConfiguredAdmin.Password), Times.Once);

        _userManager.Verify(manager => manager.AddToRoleAsync(
            It.IsAny<IdentityUser>(), Roles.Admin), Times.Once);
    }

    // An admin who already exists gets the role checked, never their password
    // rewritten — a stale value in config would otherwise reset it on every
    // single start, silently undoing any change made through the app.
    [Fact]
    public async Task Promotes_an_existing_account_without_touching_its_password()
    {
        var existing = new IdentityUser { UserName = ConfiguredAdmin.UserName };

        GivenRolesExist(true);
        GivenAdminUser(existing);
        _userManager.Setup(manager => manager.IsInRoleAsync(existing, Roles.Admin))
                    .ReturnsAsync(false);
        _userManager.Setup(manager => manager.AddToRoleAsync(existing, Roles.Admin))
                    .ReturnsAsync(IdentityResult.Success);

        await CreateSut(ConfiguredAdmin).SeedAsync();

        _userManager.Verify(manager => manager.AddToRoleAsync(existing, Roles.Admin), Times.Once);
        _userManager.Verify(manager => manager.CreateAsync(
            It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
        _userManager.Verify(manager => manager.ResetPasswordAsync(
            It.IsAny<IdentityUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Grants_nothing_to_an_account_that_is_already_an_admin()
    {
        var existing = new IdentityUser { UserName = ConfiguredAdmin.UserName };

        GivenRolesExist(true);
        GivenAdminUser(existing);
        _userManager.Setup(manager => manager.IsInRoleAsync(existing, Roles.Admin))
                    .ReturnsAsync(true);

        await CreateSut(ConfiguredAdmin).SeedAsync();

        _userManager.Verify(manager => manager.AddToRoleAsync(
            It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
    }

    // No credentials in config means no administrator — emphatically not a
    // default one. A seeded fallback password would be a back door on every
    // deployment that never got round to setting the secrets.
    [Theory]
    [InlineData("", "ada@example.com", "Sup3r-secret!")]
    [InlineData("ada", "", "Sup3r-secret!")]
    [InlineData("ada", "ada@example.com", "")]
    public async Task Creates_no_administrator_when_the_settings_are_incomplete(
        string userName, string email, string password)
    {
        GivenRolesExist(true);

        await CreateSut(new AdminAccountSettings
        {
            UserName = userName,
            Email = email,
            Password = password
        }).SeedAsync();

        _userManager.Verify(manager => manager.CreateAsync(
            It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
        _userManager.Verify(manager => manager.AddToRoleAsync(
            It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
    }

    // A failed create must not be followed by a grant against a user row that
    // was never written.
    [Fact]
    public async Task Does_not_grant_the_role_when_the_account_could_not_be_created()
    {
        GivenRolesExist(true);
        GivenAdminUser(null);
        _userManager.Setup(manager => manager.CreateAsync(
                        It.IsAny<IdentityUser>(), It.IsAny<string>()))
                    .ReturnsAsync(IdentityResult.Failed(
                        new IdentityError { Description = "Passwords must have at least one digit." }));

        await CreateSut(ConfiguredAdmin).SeedAsync();

        _userManager.Verify(manager => manager.AddToRoleAsync(
            It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
    }
}
