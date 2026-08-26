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
}
