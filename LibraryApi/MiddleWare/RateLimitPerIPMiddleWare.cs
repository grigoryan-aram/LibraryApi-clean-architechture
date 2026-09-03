namespace LibraryApi.MiddleWares;

public class RateLimitPerIPMiddleWare
{

    private readonly RequestDelegate _next;

    private static readonly Dictionary<string, int> _Requests = new();

    public RateLimitPerIPMiddleWare(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!_Requests.ContainsKey(ipAddress))
        {
            _Requests[ipAddress] = 0;
        }

        _Requests[ipAddress]++;

        if (_Requests[ipAddress] > 50)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync("Too many requests. Please try again later.");
            return;
        }
        await _next(context);
    }
}

