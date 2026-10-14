using Infrastructure.Services;
using LibraryApi.Domain.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Infrastructure.UnitTests.Services;

public class IdentityServiceTests
{
    private readonly Mock<UserManager<IdentityUser>> _userManager =
        new(Mock.Of<IUserStore<IdentityUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);

    private IdentityService CreateSut() =>
        new(_userManager.Object,
            MockSignInManager(_userManager.Object).Object,
            NullLogger<IdentityService>.Instance);

    // Only there to satisfy the constructor — none of these tests sign in.
    // SignInManager's own constructor rejects nulls for the first three, so
    // they have to be real objects.
    private static Mock<SignInManager<IdentityUser>> MockSignInManager(
        UserManager<IdentityUser> userManager) =>
        new(userManager,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            Options.Create(new IdentityOptions()),
            Mock.Of<ILogger<SignInManager<IdentityUser>>>(),
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<IdentityUser>>());

    private void GivenCreateReturns(IdentityResult result) =>
        _userManager.Setup(manager => manager.CreateAsync(
                        It.IsAny<IdentityUser>(), It.IsAny<string>()))
                    .ReturnsAsync(result);

    private void GivenAddToRoleReturns(IdentityResult result) =>
        _userManager.Setup(manager => manager.AddToRoleAsync(
                        It.IsAny<IdentityUser>(), It.IsAny<string>()))
                    .ReturnsAsync(result);

    // The whole role system rests on this line: if registration stops handing
    // out User, or ever starts handing out Admin, the two dashboards are
    // either closed to everybody or open to anybody with a browser.
    [Fact]
    public async Task Puts_a_newly_registered_account_in_the_user_role()
    {
        GivenCreateReturns(IdentityResult.Success);
        GivenAddToRoleReturns(IdentityResult.Success);

        var result = await CreateSut().RegisterAsync(
            "ada", "ada@example.com", "Sup3r-secret!", CancellationToken.None);

        Assert.False(result.IsError);
        _userManager.Verify(manager => manager.AddToRoleAsync(
            It.IsAny<IdentityUser>(), Roles.User), Times.Once);
        _userManager.Verify(manager => manager.AddToRoleAsync(
            It.IsAny<IdentityUser>(), Roles.Admin), Times.Never);
    }

    // The account exists by the time the role is assigned. Failing the whole
    // registration here would tell the caller it did not work while their
    // username was already taken — by them, with no way to retry.
    [Fact]
    public async Task Still_reports_success_when_the_role_could_not_be_granted()
    {
        GivenCreateReturns(IdentityResult.Success);
        GivenAddToRoleReturns(IdentityResult.Failed(
            new IdentityError { Description = "Role User does not exist." }));

        var result = await CreateSut().RegisterAsync(
            "ada", "ada@example.com", "Sup3r-secret!", CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("ada", result.Value.Username);
    }

    [Fact]
    public async Task Grants_no_role_when_the_account_was_refused()
    {
        GivenCreateReturns(IdentityResult.Failed(
            new IdentityError { Code = "DuplicateUserName", Description = "Username 'ada' is already taken." }));

        var result = await CreateSut().RegisterAsync(
            "ada", "ada@example.com", "Sup3r-secret!", CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Identity.DuplicateUserName", result.FirstError.Code);
        _userManager.Verify(manager => manager.AddToRoleAsync(
            It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FindContact_returns_the_username_and_address_for_an_account()
    {
        _userManager.Setup(m => m.FindByIdAsync("user-1"))
                    .ReturnsAsync(new IdentityUser { Id = "user-1", UserName = "ada", Email = "ada@example.com" });

        var contact = await CreateSut().FindContactAsync("user-1", CancellationToken.None);

        Assert.NotNull(contact);
        Assert.Equal("ada", contact.Username);
        Assert.Equal("ada@example.com", contact.Email);
    }

    [Fact]
    public async Task FindContact_returns_null_for_an_account_with_no_address()
    {
        _userManager.Setup(m => m.FindByIdAsync("user-1"))
                    .ReturnsAsync(new IdentityUser { Id = "user-1", UserName = "ada", Email = null });

        Assert.Null(await CreateSut().FindContactAsync("user-1", CancellationToken.None));
    }

    private static readonly IdentityUser Ada = new() { Id = "user-1", UserName = "ada" };

    [Fact]
    public async Task CheckPassword_refuses_a_wrong_password_as_unauthorized()
    {
        _userManager.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(Ada);
        _userManager.Setup(m => m.CheckPasswordAsync(Ada, "wrong")).ReturnsAsync(false);

        var result = await CreateSut().CheckPasswordAsync("user-1", "wrong", CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Unauthorized, result.FirstError.Type);
        Assert.Equal("Account.InvalidPassword", result.FirstError.Code);
    }

    // Same answer as a wrong password: the caller is confirming who they are,
    // and a vanished account should not be told apart.
    [Fact]
    public async Task CheckPassword_refuses_an_unknown_account_the_same_way()
    {
        _userManager.Setup(m => m.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((IdentityUser?)null);

        var result = await CreateSut().CheckPasswordAsync("gone", "Pa55word!", CancellationToken.None);

        Assert.Equal("Account.InvalidPassword", result.FirstError.Code);
    }

    [Fact]
    public async Task CheckPassword_accepts_the_right_password()
    {
        _userManager.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(Ada);
        _userManager.Setup(m => m.CheckPasswordAsync(Ada, "Pa55word!")).ReturnsAsync(true);

        var result = await CreateSut().CheckPasswordAsync("user-1", "Pa55word!", CancellationToken.None);

        Assert.False(result.IsError);
    }

    [Fact]
    public async Task DeleteAccount_reports_an_identity_refusal_as_unexpected()
    {
        _userManager.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(Ada);
        _userManager.Setup(m => m.DeleteAsync(Ada)).ReturnsAsync(IdentityResult.Failed(
            new IdentityError { Description = "Optimistic concurrency failure." }));

        var result = await CreateSut().DeleteAccountAsync("user-1", CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Unexpected, result.FirstError.Type);
        Assert.Equal("Account.NotDeleted", result.FirstError.Code);
    }

    [Fact]
    public async Task DeleteAccount_deletes_the_user()
    {
        _userManager.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(Ada);
        _userManager.Setup(m => m.DeleteAsync(Ada)).ReturnsAsync(IdentityResult.Success);

        var result = await CreateSut().DeleteAccountAsync("user-1", CancellationToken.None);

        Assert.False(result.IsError);
        _userManager.Verify(m => m.DeleteAsync(Ada), Times.Once);
    }
}
