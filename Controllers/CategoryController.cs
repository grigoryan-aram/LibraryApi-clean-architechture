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
    private readonly ISender _sender;

    public CategoriesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _sender.Send(new GetAllCategorysQuery());

        return Ok(categories);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategoryById(int id)
    {
        var category = await _sender.Send(new GetCategoryByIdQuery(id));

        return Ok(category);
    }



    [HttpPost]
    public async Task<IActionResult> AddCategory(AddCategoryCommand command)
    {
        var category = await _sender.Send(command);

        return Ok(category);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        await _sender.Send(new DeleteCategoryCommand(id));

        return NoContent();
    }

}