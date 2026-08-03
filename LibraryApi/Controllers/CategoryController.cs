using Application.Features.Categorys.Commands;
using Application.Features.Categorys.Query;
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
            errors => Problem(title: errors.First().Description));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategoryById(int id)
    {
        var result = await _mediator.Send(new GetCategoryByIdQuery(id));

        return result.Match(
            category => Ok(category),
            errors => Problem(title: errors.First().Description));
    }

    [HttpPost]
    public async Task<IActionResult> AddCategory(AddCategoryCommand command)
    {
        var result = await _mediator.Send(command);

        return result.Match(
            category => Ok(category),
            errors => Problem(title: errors.First().Description));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var result = await _mediator.Send(new DeleteCategoryCommand(id));

        return result.Match<IActionResult>(
            _ => NoContent(),
            errors => Problem(title: errors.First().Description));
    }
}