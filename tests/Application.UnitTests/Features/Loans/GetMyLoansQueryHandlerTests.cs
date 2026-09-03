using Application.Features.Loans.Queries;
using Application.RepositoryInterfaces;
using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Features.Loans;

public class GetMyLoansQueryHandlerTests
{
    private readonly Mock<ILoansRepository> _loans = new();
    private readonly Mock<IMembersRepository> _members = new();

    private GetMyLoansQueryHandler CreateSut() => new(_loans.Object, _members.Object, NullLogger<GetMyLoansQueryHandler>.Instance);

    private static GetMyLoansQuery Query => new("user-1");

    private void GivenTheAccountHasAMember(int memberId = 5) =>
        _members.Setup(repo => repo.GetMemberByIdentityUserIdAsync(
                    "user-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemberModel
                {
                    Id = memberId,
                    Name = "Ada",
                    IdentityUserId = "user-1"
                });

    [Fact]
    public async Task Reports_an_account_with_no_member_rather_than_an_empty_list()
    {
        _members.Setup(repo => repo.GetMemberByIdentityUserIdAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MemberModel?)null);

        var result = await CreateSut().Handle(Query, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Equal("Loans.NoMemberForAccount", result.FirstError.Code);
        _loans.Verify(repo => repo.GetLoansForMemberAsync(
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_an_empty_list_for_a_member_who_has_borrowed_nothing()
    {
        GivenTheAccountHasAMember();
        _loans.Setup(repo => repo.GetLoansForMemberAsync(5, It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);

        var result = await CreateSut().Handle(Query, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Asks_only_for_the_loans_of_the_member_behind_the_account()
    {
        GivenTheAccountHasAMember(memberId: 7);
        _loans.Setup(repo => repo.GetLoansForMemberAsync(
                  It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);

        await CreateSut().Handle(Query, CancellationToken.None);

        _loans.Verify(repo => repo.GetLoansForMemberAsync(
            7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Returns_the_loans_with_their_computed_state()
    {
        GivenTheAccountHasAMember();
        _loans.Setup(repo => repo.GetLoansForMemberAsync(5, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<LoanModel>
              {
                  new()
                  {
                      Id = 1,
                      BookId = 4,
                      MemberId = 5,
                      BorrowedAt = DateTime.UtcNow.AddDays(-20),
                      DueAt = DateTime.UtcNow.AddDays(-6),
                      ReturnedAt = null
                  },
                  new()
                  {
                      Id = 2,
                      BookId = 6,
                      MemberId = 5,
                      BorrowedAt = DateTime.UtcNow.AddDays(-3),
                      DueAt = DateTime.UtcNow.AddDays(11),
                      ReturnedAt = DateTime.UtcNow.AddDays(-1)
                  }
              });

        var result = await CreateSut().Handle(Query, CancellationToken.None);

        Assert.Equal(2, result.Value.Count);

        var overdue = result.Value.Single(loan => loan.Id == 1);
        Assert.True(overdue.IsOverdue);
        Assert.Equal(6, overdue.DaysOverdue);

        var returned = result.Value.Single(loan => loan.Id == 2);
        Assert.True(returned.IsReturned);
        Assert.False(returned.IsOverdue);
    }
}

public class GetMyLoansQueryValidatorTests
{
    private readonly GetMyLoansQueryValidator _validator = new();

    [Fact]
    public void Accepts_an_identity_user_id()
    {
        Assert.True(_validator.Validate(new GetMyLoansQuery("user-1")).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_caller_it_cannot_identify(string identityUserId)
    {
        var result = _validator.Validate(new GetMyLoansQuery(identityUserId));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(GetMyLoansQuery.IdentityUserId));
    }
}
