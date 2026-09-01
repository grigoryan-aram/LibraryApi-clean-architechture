using Application.Features.Books.Commands;
using Application.RepositoryInterfaces;
using ErrorOr;
using LibraryApi.Domain.Entities;
using Moq;

namespace Application.UnitTests.Features.Books;

public class AddBookCommandHandlerTests
{
    private readonly Mock<IBooksRepository> _books = new();
    private readonly Mock<ICategorysRepository> _categorys = new();

    private AddBookCommandHandler CreateSut() => new(_books.Object, _categorys.Object);

    private static AddBookCommand Command => new(
        Title: "Dune", Author: "Frank Herbert", CategoryId: 3, TotalCopies: 2);

    private void GivenCategoryExists() =>
        _categorys.Setup(repo => repo.GetCategoryByIdAsync(3, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new CategoryModel { Id = 3, Name = "Fiction" });

    private void GivenTheBookIsSaved() =>
        _books.Setup(repo => repo.AddAsync(It.IsAny<BookModel>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((BookModel book, CancellationToken _) =>
              {
                  book.Id = 42;
                  return book;
              });

    [Fact]
    public async Task Refuses_a_category_that_does_not_exist_without_writing_anything()
    {
        _categorys.Setup(repo => repo.GetCategoryByIdAsync(
                      It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((CategoryModel?)null);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Equal("Books.CategoryNotFound", result.FirstError.Code);
        _books.Verify(repo => repo.AddAsync(
            It.IsAny<BookModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Stores_the_requested_number_of_copies()
    {
        GivenCategoryExists();
        GivenTheBookIsSaved();

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(42, result.Value.Id);
        Assert.Equal(2, result.Value.TotalCopies);
        _books.Verify(repo => repo.AddAsync(
            It.Is<BookModel>(book =>
                book.Title == "Dune"
                && book.Author == "Frank Herbert"
                && book.CategoryId == 3
                && book.TotalCopies == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reports_a_new_book_as_fully_available()
    {
        GivenCategoryExists();
        GivenTheBookIsSaved();

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.Equal(0, result.Value.CopiesOnLoan);
        Assert.Equal(2, result.Value.AvailableCopies);
        Assert.True(result.Value.IsAvailable);
    }
}
