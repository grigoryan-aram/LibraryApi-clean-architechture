using Application.Features.Books.Queries;
using Application.RepositoryInterfaces;
using LibraryApi.Domain.Entities;
using Moq;

namespace Application.UnitTests.Features.Books;

public class GetAllBooksQueryHandlerTests
{
    private readonly Mock<IBooksRepository> _books = new();
    private readonly Mock<ILoansRepository> _loans = new();

    private GetAllBooksQueryHandler CreateSut() => new(_books.Object, _loans.Object);

    private void GivenTheCatalogue(params BookModel[] books) =>
        _books.Setup(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(books);

    private void GivenCopiesOnLoan(Dictionary<int, int> counts) =>
        _loans.Setup(repo => repo.CountActiveLoansByBookAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(counts);

    private static BookModel Book(int id, int totalCopies) => new()
    {
        Id = id,
        Title = $"Book {id}",
        Author = "Someone",
        CategoryId = 1,
        TotalCopies = totalCopies
    };

    [Fact]
    public async Task Maps_the_stored_fields_onto_the_dto()
    {
        GivenTheCatalogue(new BookModel
        {
            Id = 4,
            Title = "The Hobbit",
            Author = "J.R.R. Tolkien",
            CategoryId = 2,
            TotalCopies = 2
        });
        GivenCopiesOnLoan([]);

        var result = await CreateSut().Handle(new GetAllBooksQuery(), CancellationToken.None);

        var book = Assert.Single(result.Value);
        Assert.Equal(4, book.Id);
        Assert.Equal("The Hobbit", book.Title);
        Assert.Equal("J.R.R. Tolkien", book.Author);
        Assert.Equal(2, book.CategoryId);
        Assert.Equal(2, book.TotalCopies);
    }

    [Fact]
    public async Task Fills_in_the_on_loan_count_for_each_book()
    {
        GivenTheCatalogue(Book(1, totalCopies: 3), Book(2, totalCopies: 1));
        GivenCopiesOnLoan(new Dictionary<int, int> { [1] = 2, [2] = 1 });

        var result = await CreateSut().Handle(new GetAllBooksQuery(), CancellationToken.None);

        var first = result.Value.Single(b => b.Id == 1);
        Assert.Equal(2, first.CopiesOnLoan);
        Assert.Equal(1, first.AvailableCopies);
        Assert.True(first.IsAvailable);

        var second = result.Value.Single(b => b.Id == 2);
        Assert.Equal(1, second.CopiesOnLoan);
        Assert.Equal(0, second.AvailableCopies);
        Assert.False(second.IsAvailable);
    }

    [Fact]
    public async Task Treats_a_book_missing_from_the_counts_as_having_nothing_out()
    {
        GivenTheCatalogue(Book(7, totalCopies: 2));
        GivenCopiesOnLoan(new Dictionary<int, int> { [99] = 4 });

        var result = await CreateSut().Handle(new GetAllBooksQuery(), CancellationToken.None);

        var book = Assert.Single(result.Value);
        Assert.Equal(0, book.CopiesOnLoan);
        Assert.Equal(2, book.AvailableCopies);
    }

    [Fact]
    public async Task Never_reports_a_negative_number_of_available_copies()
    {
        GivenTheCatalogue(Book(1, totalCopies: 1));
        GivenCopiesOnLoan(new Dictionary<int, int> { [1] = 4 });

        var result = await CreateSut().Handle(new GetAllBooksQuery(), CancellationToken.None);

        var book = Assert.Single(result.Value);
        Assert.Equal(0, book.AvailableCopies);
        Assert.False(book.IsAvailable);
    }

    [Fact]
    public async Task Counts_the_open_loans_in_a_single_query()
    {
        GivenTheCatalogue(Book(1, 1), Book(2, 1), Book(3, 1));
        GivenCopiesOnLoan([]);

        await CreateSut().Handle(new GetAllBooksQuery(), CancellationToken.None);

        _loans.Verify(repo => repo.CountActiveLoansByBookAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        _loans.Verify(repo => repo.CountActiveLoansForBookAsync(
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
