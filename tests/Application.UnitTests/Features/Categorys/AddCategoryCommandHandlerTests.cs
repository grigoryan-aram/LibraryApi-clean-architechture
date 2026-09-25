using Application.Features.Categorys.Commands;
using ErrorOr;
using LibraryApi.Domain.Entities;
using LibraryApi.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Features.Categorys;

public class AddCategoryCommandHandlerTests
{
    private readonly Mock<ICategorysRepository> _categorys = new();

    private AddCategoryCommandHandler CreateSut() =>
        new(_categorys.Object, NullLogger<AddCategoryCommandHandler>.Instance);

    private static AddCategoryCommand Command => new(Name: "Science Fiction");

    // Recorded inside the stub rather than asserted with It.Is<> at Verify time:
    // the stub mutates the entity it is handed, and Moq re-runs matchers against
    // the mutated instance.
    private string? _namePassedToRepository;

    private void GivenTheCategoryIsSaved() =>
        _categorys.Setup(repo => repo.AddCategoryAsync(
                      It.IsAny<CategoryModel>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((CategoryModel category, CancellationToken _) =>
                  {
                      _namePassedToRepository = category.Name;
                      category.Id = 7;
                      return category;
                  });

    // The regression test for this slice. AddCategoryCommand used to declare
    // "title", which Mapster could not match to CategoryModel.Name, so every
    // category reached the database with an empty name and the API happily
    // answered 200. Nothing in the compiler catches a convention-based mapping
    // that stops matching.
    [Fact]
    public async Task Maps_the_name_onto_the_entity_it_saves()
    {
        GivenTheCategoryIsSaved();

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("Science Fiction", _namePassedToRepository);
        Assert.Equal("Science Fiction", result.Value.Name);
    }

    // The identity column assigns the id, so whatever the caller sends is
    // irrelevant — the command no longer has an id to send.
    [Fact]
    public async Task Returns_the_id_the_database_assigned()
    {
        GivenTheCategoryIsSaved();

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.Equal(7, result.Value.id);
    }

    [Fact]
    public async Task Reports_a_failure_when_the_repository_saves_nothing()
    {
        _categorys.Setup(repo => repo.AddCategoryAsync(
                      It.IsAny<CategoryModel>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((CategoryModel)null!);

        var result = await CreateSut().Handle(Command, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
        Assert.Equal("Category.Add", result.FirstError.Code);
    }
}
