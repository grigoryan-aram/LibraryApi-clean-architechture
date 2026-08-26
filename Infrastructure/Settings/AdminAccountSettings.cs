namespace Infrastructure.Settings
{
    /// <summary>
    /// The seed administrator, bound from the <c>Identity:Admin</c> section.
    ///
    /// All three values ship empty in appsettings*.json — a password in source
    /// control would be a back door on every deployment. Set them as user
    /// secrets locally, or in appsettings.Secrets.json / environment variables
    /// on the host. With no username configured the seeder creates the roles
    /// and stops, leaving the site with no administrator.
    /// </summary>
    public class AdminAccountSettings
    {
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(UserName)
            && !string.IsNullOrWhiteSpace(Email)
            && !string.IsNullOrWhiteSpace(Password);
    }
}
