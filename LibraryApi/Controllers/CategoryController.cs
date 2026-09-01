using Application.Features.Categorys.Commands;
using Application.Features.Categorys.Query;
using LibraryApi.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryApi.Controllers;


[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CategoriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _mediator.Send(new GetAllCategorysQuery());

        return result.Match(
            categories => Ok(categories),
            errors => this.ToProblem(errors));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategoryById(int id)
    {
        var result = await _mediator.Send(new GetCategoryByIdQuery(id));

        return result.Match(
            category => Ok(category),
            errors => this.ToProblem(errors));
    }

    [HttpPost]
    public async Task<IActionResult> AddCategory(AddCategoryCommand command)
    {
        var result = await _mediator.Send(command);

        return result.Match(
            category => Ok(category),
            errors => this.ToProblem(errors));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCategory(int id, UpdateCategoryCommand command)
    {
        var result = await _mediator.Send(command with { Id = id });

        return result.Match(
            category => Ok(category),
            errors => this.ToProblem(errors));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var result = await _mediator.Send(new DeleteCategoryCommand(id));

        return result.Match(
            _ => NoContent(),
            errors => this.ToProblem(errors));
    }
}
