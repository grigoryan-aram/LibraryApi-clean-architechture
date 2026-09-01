using Application.Features.Books.Commands;
using Application.RepositoryInterfaces;
using ErrorOr;
using LibraryApi.Domain.Entities;
using Moq;

namespace Application.UnitTests.Features.Books;

public class UpdateBookCommandHandlerTests
{
    private readonly Mock<IBooksRepository> _books = new();
    private readonly Mock<ICategorysRepository> _categorys = new();
    private readonly Mock<ILoansRepository> _loans = new();

    private UpdateBookCommandHandler CreateSut() =>
        new(_books.Object, _categorys.Object, _loans.Object);

    private static UpdateBookCommand Command(int totalCopies = 2) =>
        new(Id: 1, Title: "Dune", Author: "Frank Herbert", CategoryId: 3, TotalCopies: totalCopies);

    private void GivenBookExists(int totalCopies = 1) =>
        _books.Setup(repo => repo.GetByIdAsync(1, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new BookModel
              {
                  Id = 1,
                  Title = "Dune, first edition",
                  Author = "F. Herbert",
                  CategoryId = 9,
                  TotalCopies = totalCopies
              });

    private void GivenCategoryExists() =>
        _categorys.Setup(repo => repo.GetCategoryByIdAsync(3, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new CategoryModel { Id = 3, Name = "Fiction" });

    private void GivenCopiesOnLoan(int count) =>
        _loans.Setup(repo => repo.CountActiveLoansForBookAsync(1, It.IsAny<CancellationToken>()))
              .ReturnsAsync(count);

    private void GivenTheBookIsSaved() =>
        _books.Setup(repo => repo.UpdateAsync(It.IsAny<BookModel>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((BookModel book, CancellationToken _) => book);

    [Fact]
    public async Task Refuses_a_book_that_does_not_exist()
    {
        _books.Setup(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((BookModel?)null);

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Equal("Books.NotFound", result.FirstError.Code);
        _books.Verify(repo => repo.UpdateAsync(
            It.IsAny<BookModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refuses_a_category_that_does_not_exist()
    {
        GivenBookExists();
        _categorys.Setup(repo => repo.GetCategoryByIdAsync(
                      It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((CategoryModel?)null);

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Equal("Books.CategoryNotFound", result.FirstError.Code);
        _books.Verify(repo => repo.UpdateAsync(
            It.IsAny<BookModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Writes_the_new_title_author_category_and_copies()
    {
        GivenBookExists();
        GivenCategoryExists();
        GivenTheBookIsSaved();

        var result = await CreateSut().Handle(Command(totalCopies: 4), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("Dune", result.Value.Title);
        Assert.Equal("Frank Herbert", result.Value.Author);
        Assert.Equal(3, result.Value.CategoryId);
        Assert.Equal(4, result.Value.TotalCopies);
    }

    [Fact]
    public async Task Refuses_to_cut_total_copies_below_the_number_on_loan()
    {
        GivenBookExists(totalCopies: 5);
        GivenCategoryExists();
        GivenCopiesOnLoan(3);

        var result = await CreateSut().Handle(Command(totalCopies: 2), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Conflict, result.FirstError.Type);
        Assert.Equal("Books.CopiesBelowActiveLoans", result.FirstError.Code);
        Assert.Contains("3 copies are currently on loan", result.FirstError.Description);
        _books.Verify(repo => repo.UpdateAsync(
            It.IsAny<BookModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Allows_cutting_total_copies_down_to_exactly_the_number_on_loan()
    {
        GivenBookExists(totalCopies: 5);
        GivenCategoryExists();
        GivenCopiesOnLoan(2);
        GivenTheBookIsSaved();

        var result = await CreateSut().Handle(Command(totalCopies: 2), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(2, result.Value.TotalCopies);
        Assert.Equal(0, result.Value.AvailableCopies);
        Assert.False(result.Value.IsAvailable);
    }

    [Fact]
    public async Task Reports_how_many_copies_are_on_loan()
    {
        GivenBookExists(totalCopies: 5);
        GivenCategoryExists();
        GivenCopiesOnLoan(2);
        GivenTheBookIsSaved();

        var result = await CreateSut().Handle(Command(totalCopies: 5), CancellationToken.None);

        Assert.Equal(2, result.Value.CopiesOnLoan);
        Assert.Equal(3, result.Value.AvailableCopies);
    }
}
