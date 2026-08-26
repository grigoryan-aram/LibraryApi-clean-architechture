namespace LibraryApi.Domain.Constants;

/// <summary>
/// The two roles this application knows about. They are seeded at startup by
/// the Infrastructure identity seeder; nothing creates them on demand, so a
/// name added here without a matching seed line will make
/// <c>AddToRoleAsync</c> throw at runtime.
/// </summary>
public static class Roles
{
    /// <summary>
    /// Full access, including the Swagger UI and the Hangfire dashboard.
    /// Granted only by the seeder — registration never hands it out.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Every account created through registration. Enough for the API, the
    /// chat page and the browsable pages; not enough for the two dashboards.
    /// </summary>
    public const string User = "User";

    public static readonly IReadOnlyList<string> All = [Admin, User];
}
