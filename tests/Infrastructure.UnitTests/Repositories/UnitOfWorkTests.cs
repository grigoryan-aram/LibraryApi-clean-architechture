using ErrorOr;
using Infrastructure.Repositories;
using LibraryApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.UnitTests.Repositories;

// SQLite, not InMemory: the InMemory provider ignores transactions entirely,
// so it could not tell a commit from a rollback.
public class UnitOfWorkTests : IDisposable
{
    private readonly SqliteDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private async Task<ErrorOr<int>> AddMember(string name, CancellationToken token)
    {
        var member = new MemberModel { Name = name };
        _db.Context.Members.Add(member);
        await _db.Context.SaveChangesAsync(token);
        return member.Id;
    }

    private async Task<int> MembersOnDisk()
    {
        await using var context = _db.NewContext();
        return await context.Members.CountAsync();
    }

    [Fact]
    public async Task Commits_when_the_work_returns_a_value()
    {
        var result = await new UnitOfWork(_db.Context).ExecuteInTransactionAsync(
            token => AddMember("ada", token), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(1, await MembersOnDisk());
    }

    // Even though SaveChanges ran and the row was written inside the
    // transaction — that is the case the outbox depends on.
    [Fact]
    public async Task Rolls_back_saved_rows_when_the_work_returns_an_error()
    {
        var result = await new UnitOfWork(_db.Context).ExecuteInTransactionAsync<int>(
            async token =>
            {
                await AddMember("ada", token);
                return Error.Unexpected("Events.NotRecorded", "no");
            },
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(0, await MembersOnDisk());
    }

    [Fact]
    public async Task Rolls_back_when_the_work_throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UnitOfWork(_db.Context).ExecuteInTransactionAsync<int>(
                async token =>
                {
                    await AddMember("ada", token);
                    throw new InvalidOperationException("boom");
                },
                CancellationToken.None));

        Assert.Equal(0, await MembersOnDisk());
    }

    [Fact]
    public async Task Joins_a_transaction_that_is_already_open_and_leaves_it_to_the_owner()
    {
        await using var outer = await _db.Context.Database.BeginTransactionAsync();

        await new UnitOfWork(_db.Context).ExecuteInTransactionAsync(
            token => AddMember("ada", token), CancellationToken.None);

        Assert.NotNull(_db.Context.Database.CurrentTransaction);

        await outer.RollbackAsync();

        Assert.Equal(0, await MembersOnDisk());
    }
}
