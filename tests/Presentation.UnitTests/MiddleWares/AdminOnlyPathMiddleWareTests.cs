using System.Security.Claims;
using LibraryApi.Domain.Constants;
using LibraryApi.MiddleWares;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Presentation.UnitTests.MiddleWares;

public class AdminOnlyPathMiddleWareTests
{
    private readonly Mock<IAuthenticationService> _authentication = new();
    private bool _reachedTheApp;

    // The gate hands off to the cookie handler through IAuthenticationService,
    // so that is what tells us whether it challenged or forbade.
    private async Task<HttpContext> Run(string path, ClaimsPrincipal user)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_authentication.Object);

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = user
        };

        context.Request.Path = path;

        var sut = new AdminOnlyPathMiddleWare(
            _ =>
            {
                _reachedTheApp = true;
                return Task.CompletedTask;
            },
            new PathString("/swagger"));

        await sut.InvokeAsync(context);

        return context;
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal SignedIn(params string[] roles) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "ada"),
             .. roles.Select(role => new Claim(ClaimTypes.Role, role))],
            authenticationType: "Cookies"));

    private void AssertChallenged(Times times) =>
        _authentication.Verify(auth => auth.ChallengeAsync(
            It.IsAny<HttpContext>(), It.IsAny<string>(),
            It.IsAny<AuthenticationProperties>()), times);

    private void AssertForbidden(Times times) =>
        _authentication.Verify(auth => auth.ForbidAsync(
            It.IsAny<HttpContext>(), It.IsAny<string>(),
            It.IsAny<AuthenticationProperties>()), times);

    [Fact]
    public async Task Lets_an_admin_through()
    {
        await Run("/swagger/index.html", SignedIn(Roles.Admin));

        Assert.True(_reachedTheApp);
        AssertChallenged(Times.Never());
        AssertForbidden(Times.Never());
    }

    // Challenge rather than a bare 401, so a browser is sent to the login page
    // while curl still gets its status code.
    [Fact]
    public async Task Challenges_an_anonymous_visitor()
    {
        await Run("/swagger/index.html", Anonymous());

        Assert.False(_reachedTheApp);
        AssertChallenged(Times.Once());
    }

    // Anyone can create an account through /api/Auth/register, so being signed
    // in is no bar at all — this is the case the whole role system exists for.
    [Fact]
    public async Task Forbids_a_signed_in_user_who_is_not_an_admin()
    {
        await Run("/swagger/index.html", SignedIn(Roles.User));

        Assert.False(_reachedTheApp);
        AssertForbidden(Times.Once());
        AssertChallenged(Times.Never());
    }

    // The JSON document describes every endpoint in the app; gating only the
    // UI page would leave it readable.
    [Fact]
    public async Task Gates_the_swagger_document_as_well_as_the_ui()
    {
        await Run("/swagger/v1/swagger.json", SignedIn(Roles.User));

        Assert.False(_reachedTheApp);
        AssertForbidden(Times.Once());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/api/Books")]
    [InlineData("/swaggerish")]   // StartsWithSegments, not StartsWith
    public async Task Leaves_every_other_path_alone(string path)
    {
        await Run(path, Anonymous());

        Assert.True(_reachedTheApp);
        AssertChallenged(Times.Never());
        AssertForbidden(Times.Never());
    }
}
