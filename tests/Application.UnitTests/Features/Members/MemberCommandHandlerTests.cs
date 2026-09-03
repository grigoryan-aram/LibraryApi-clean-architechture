using Application.Features.Members.Commands;
using ErrorOr;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Features.Members;

public class AddMemberCommandHandlerTests
{
    private readonly Mock<IMembersRepository> _members = new();

    private int _idPassedToRepository = -1;

    private AddMemberCommandHandler CreateSut() =>
        new(_members.Object, NullLogger<AddMemberCommandHandler>.Instance);

    private void GivenTheMemberIsSaved() =>
        _members.Setup(repo => repo.AddMemberAsync(
                    It.IsAny<MemberModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MemberModel member, CancellationToken _) =>
                {
                    _idPassedToRepository = member.Id;

                    member.Id = 5;
                    return member;
                });

    [Fact]
    public async Task Leaves_the_id_for_the_database_to_assign()
    {
        GivenTheMemberIsSaved();

        var result = await CreateSut().Handle(new AddMemberCommand("Ada Lovelace"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(5, result.Value.id);
        Assert.Equal(0, _idPassedToRepository);
        _members.Verify(repo => repo.AddMemberAsync(
            It.Is<MemberModel>(member => member.Name == "Ada Lovelace"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Does_not_attach_an_identity_account()
    {
        GivenTheMemberIsSaved();

        await CreateSut().Handle(new AddMemberCommand("Ada Lovelace"), CancellationToken.None);

        _members.Verify(repo => repo.AddMemberAsync(
            It.Is<MemberModel>(member => member.IdentityUserId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class UpdateMemberCommandHandlerTests
{
    private readonly Mock<IMembersRepository> _members = new();

    private UpdateMemberCommandHandler CreateSut() =>
        new(_members.Object, NullLogger<UpdateMemberCommandHandler>.Instance);

    private static UpdateMemberCommand Command => new(Id: 5, Name: "Ada Byron");

    [Fact]
    public async Task Refuses_a_member_that_does_not_exist()
    {
        _members.Setup(repo => repo.GetMemberByIdAsync(
                    It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MemberModel?)null);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Equal("Members.NotFound", result.FirstError.Code);
        _members.Verify(repo => repo.UpdateMemberAsync(
            It.IsAny<MemberModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Renames_a_member_without_touching_their_account_link()
    {
        _members.Setup(repo => repo.GetMemberByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemberModel
                {
                    Id = 5,
                    Name = "Ada Lovelace",
                    IdentityUserId = "user-1"
                });
        _members.Setup(repo => repo.UpdateMemberAsync(
                    It.IsAny<MemberModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MemberModel member, CancellationToken _) => member);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("Ada Byron", result.Value.Name);
        _members.Verify(repo => repo.UpdateMemberAsync(
            It.Is<MemberModel>(member =>
                member.Name == "Ada Byron" && member.IdentityUserId == "user-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
