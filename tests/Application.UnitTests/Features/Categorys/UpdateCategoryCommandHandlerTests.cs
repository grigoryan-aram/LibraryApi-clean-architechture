using Application.Features.Categorys.Commands;
using Application.RepositoryInterfaces;
using ErrorOr;
using LibraryApi.Domain.Entities;
using Moq;

namespace Application.UnitTests.Features.Categorys;

public class UpdateCategoryCommandHandlerTests
{
    private readonly Mock<ICategorysRepository> _categorys = new();

    private UpdateCategoryCommandHandler CreateSut() => new(_categorys.Object);

    private static UpdateCategoryCommand Command => new(Id: 3, Name: "Science Fiction");

    [Fact]
    public async Task Refuses_a_category_that_does_not_exist()
    {
        _categorys.Setup(repo => repo.GetCategoryByIdAsync(
                      It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((CategoryModel?)null);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Equal("Categorys.NotFound", result.FirstError.Code);
        _categorys.Verify(repo => repo.UpdateCategoryAsync(
            It.IsAny<CategoryModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Renames_an_existing_category()
    {
        _categorys.Setup(repo => repo.GetCategoryByIdAsync(3, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new CategoryModel { Id = 3, Name = "Fiction" });
        _categorys.Setup(repo => repo.UpdateCategoryAsync(
                      It.IsAny<CategoryModel>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((CategoryModel category, CancellationToken _) => category);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(3, result.Value.id);
        Assert.Equal("Science Fiction", result.Value.Name);
        _categorys.Verify(repo => repo.UpdateCategoryAsync(
            It.Is<CategoryModel>(category =>
                category.Id == 3 && category.Name == "Science Fiction"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
