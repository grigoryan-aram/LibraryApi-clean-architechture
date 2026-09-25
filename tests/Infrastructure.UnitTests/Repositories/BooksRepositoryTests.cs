using Infrastructure.Repositories;
using LibraryApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.UnitTests.Repositories;

public class BooksRepositoryTests : IDisposable
{
    private readonly SqliteDatabase _database = new();

    private BooksRepository CreateSut() => new(_database.Context);

    public void Dispose() => _database.Dispose();

    private async Task GivenBooks(params (string Title, string Author)[] books)
    {
        var category = new CategoryModel { Name = "Fiction" };
        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        foreach (var (title, author) in books)
        {
            _database.Context.Books.Add(new BookModel
            {
                Title = title,
                Author = author,
                CategoryId = category.Id,
                TotalCopies = 1
            });
        }

        await _database.Context.SaveChangesAsync();
        _database.Context.ChangeTracker.Clear();
    }

    private static (string, string)[] TenBooks =>
        Enumerable.Range(1, 10)
            .Select(i => ($"Title {i:00}", $"Author {i:00}"))
            .ToArray();

    [Fact]
    public async Task Search_returns_the_requested_page_and_the_full_count()
    {
        await GivenBooks(TenBooks);

        var (items, totalCount) = await CreateSut()
            .SearchAsync(null, null, false, skip: 4, take: 3, CancellationToken.None);

        Assert.Equal(3, items.Count);
        Assert.Equal(10, totalCount);
    }

    // Skip/Take on an unordered query has no defined row order in SQL, so the
    // repository always orders by something. Without that, page 2 can legally
    // repeat a row from page 1.
    [Fact]
    public async Task Search_pages_without_repeating_or_dropping_a_row()
    {
        await GivenBooks(TenBooks);

        var sut = CreateSut();

        var (first, _) = await sut.SearchAsync(null, null, false, 0, 4, CancellationToken.None);
        var (second, _) = await sut.SearchAsync(null, null, false, 4, 4, CancellationToken.None);
        var (third, _) = await sut.SearchAsync(null, null, false, 8, 4, CancellationToken.None);

        var seen = first.Concat(second).Concat(third).Select(b => b.Id).ToList();

        Assert.Equal(10, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    [Fact]
    public async Task Search_matches_on_title_and_on_author()
    {
        await GivenBooks(
            ("Dune", "Frank Herbert"),
            ("Neuromancer", "William Gibson"),
            ("Herbert the Hedgehog", "Someone Else"));

        var sut = CreateSut();

        var (byTitle, titleCount) = await sut
            .SearchAsync("Neuro", null, false, 0, 20, CancellationToken.None);
        var (byAuthor, authorCount) = await sut
            .SearchAsync("Herbert", null, false, 0, 20, CancellationToken.None);

        Assert.Equal(1, titleCount);
        Assert.Equal("Neuromancer", byTitle[0].Title);

        // Matches the author of one and the title of another.
        Assert.Equal(2, authorCount);
        Assert.Equal(2, byAuthor.Count);
    }

    // The count has to be taken before paging, or it reports how many rows
    // came back rather than how many matched.
    [Fact]
    public async Task Search_counts_every_match_not_just_the_page()
    {
        await GivenBooks(TenBooks);

        var (items, totalCount) = await CreateSut()
            .SearchAsync("Title", null, false, 0, 2, CancellationToken.None);

        Assert.Equal(2, items.Count);
        Assert.Equal(10, totalCount);
    }

    [Theory]
    [InlineData("title", false, "Title 01")]
    [InlineData("title", true, "Title 10")]
    [InlineData("author", false, "Title 01")]
    [InlineData("author", true, "Title 10")]
    public async Task Search_sorts_by_the_requested_key(
        string sortBy, bool descending, string expectedFirstTitle)
    {
        await GivenBooks(TenBooks);

        var (items, _) = await CreateSut()
            .SearchAsync(null, sortBy, descending, 0, 20, CancellationToken.None);

        Assert.Equal(expectedFirstTitle, items[0].Title);
    }

    [Fact]
    public async Task Search_returns_an_empty_page_when_nothing_matches()
    {
        await GivenBooks(TenBooks);

        var (items, totalCount) = await CreateSut()
            .SearchAsync("no such book", null, false, 0, 20, CancellationToken.None);

        Assert.Empty(items);
        Assert.Equal(0, totalCount);
    }

    // ExecuteDeleteAsync is relational-only, which is the whole reason these
    // tests run on SQLite rather than the InMemory provider.
    [Fact]
    public async Task Delete_removes_the_row()
    {
        await GivenBooks(("Dune", "Frank Herbert"));
        var id = await _database.Context.Books.Select(b => b.Id).FirstAsync();

        await CreateSut().DeleteAsync(id, CancellationToken.None);

        await using var reader = _database.NewContext();
        Assert.False(await reader.Books.AnyAsync(b => b.Id == id));
    }

    // Deleting a missing id is a silent no-op by design — ExecuteDeleteAsync
    // does not load first, so there is nothing to find missing.
    [Fact]
    public async Task Delete_is_a_no_op_for_an_id_that_does_not_exist()
    {
        await GivenBooks(TenBooks);

        await CreateSut().DeleteAsync(9999, CancellationToken.None);

        await using var reader = _database.NewContext();
        Assert.Equal(10, await reader.Books.CountAsync());
    }

    // Every read is AsNoTracking, so UpdateAsync has to call Update() to
    // attach the detached instance. A bare SaveChangesAsync would report
    // success and persist nothing.
    [Fact]
    public async Task Update_persists_a_change_to_a_detached_entity()
    {
        await GivenBooks(("Dune", "Frank Herbert"));
        var sut = CreateSut();

        var book = await sut.GetByIdAsync(
            await _database.Context.Books.Select(b => b.Id).FirstAsync(),
            CancellationToken.None);

        book!.Title = "Dune, second edition";
        await sut.UpdateAsync(book, CancellationToken.None);

        await using var reader = _database.NewContext();
        var reloaded = await reader.Books.SingleAsync(b => b.Id == book.Id);
        Assert.Equal("Dune, second edition", reloaded.Title);
    }

    [Fact]
    public async Task Add_assigns_the_identity_and_persists_the_row()
    {
        var category = new CategoryModel { Name = "Fiction" };
        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        var saved = await CreateSut().AddAsync(
            new BookModel
            {
                Title = "Dune",
                Author = "Frank Herbert",
                CategoryId = category.Id,
                TotalCopies = 2
            },
            CancellationToken.None);

        Assert.True(saved.Id > 0);

        await using var reader = _database.NewContext();
        Assert.Equal("Dune", (await reader.Books.SingleAsync(b => b.Id == saved.Id)).Title);
    }
}
