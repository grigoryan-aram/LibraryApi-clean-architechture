using LibraryApi.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LibraryApi.HealthChecks;

/// <summary>
/// Reports whether the application can actually reach its database.
/// </summary>
/// <remarks>
/// Hand-written rather than pulled in from
/// Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore: the
/// whole check is one call, and a liveness probe that answers Healthy while
/// SQL Server is unreachable is worse than no probe at all.
/// </remarks>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly LibraryDBContext _dbContext;

    public DatabaseHealthCheck(LibraryDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reachable = await _dbContext.Database
                .CanConnectAsync(cancellationToken);

            return reachable
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Database unreachable.");
        }
        catch (Exception exception)
        {
            // CanConnectAsync swallows most provider errors, but a bad
            // connection string throws outright. The endpoint must answer
            // Unhealthy rather than surface a 500 from the probe itself.
            return HealthCheckResult.Unhealthy(
                "Database unreachable.",
                exception);
        }
    }
}
