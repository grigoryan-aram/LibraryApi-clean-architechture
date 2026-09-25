using ErrorOr;
using Microsoft.AspNetCore.Mvc;

namespace LibraryApi.Extensions
{
    public static class ErrorExtensions
    {
        /// <summary>
        /// The status code an <see cref="ErrorType"/> answers with. Separated
        /// from <see cref="ToProblem"/> so the mapping can be tested without
        /// standing up a ControllerBase and a ProblemDetailsFactory.
        /// </summary>
        public static int StatusCodeFor(ErrorType type) => type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
            ErrorType.Validation => StatusCodes.Status400BadRequest,

            // Failure is ErrorOr's catch-all and this codebase leans on it for
            // a mixture of causes — a missing Claude API key, a spent
            // allowance, a repository that saved no row. Several of those are
            // really 500s or a 429, but they are mislabelled at the source
            // rather than here, so they keep answering 400 until the errors
            // themselves are retyped.
            _ => StatusCodes.Status400BadRequest
        };

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

            var first = errors.First();

            return controller.Problem(
                title: first.Description,
                statusCode: StatusCodeFor(first.Type));
        }
    }
}
