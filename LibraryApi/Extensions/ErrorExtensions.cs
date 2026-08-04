using ErrorOr;
using Microsoft.AspNetCore.Mvc;

namespace LibraryApi.Extensions
{
    public static class ErrorExtensions
    {
        public static IActionResult ToProblem(
            this ControllerBase controller,
            List<Error> errors)
        {
            if (errors.All(e => e.Type == ErrorType.Validation))
            {
                var validationErrors = errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.Description).ToArray());

                var details = new ValidationProblemDetails(validationErrors)
                {
                    Title = "One or more validation errors occurred.",
                    Status = StatusCodes.Status400BadRequest
                };

                return new BadRequestObjectResult(details);
            }

            return controller.Problem(
                title: errors.First().Description,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}