using System.Data;
using System.Data.Common;
using Hangfire.SqlServer;

namespace Infrastructure
{
    /// <summary>
    /// Installs Hangfire's own tables, explicitly and at a point where the
    /// database is known to exist.
    /// </summary>
    public static class HangfireSchema
    {
        /// <summary>
        /// Safe on every start: the installer checks its own schema version and
        /// does nothing when the tables are already there.
        /// </summary>
        /// <param name="connection">
        /// The DbContext's own connection, taken straight after
        /// <c>Database.Migrate()</c>. Deliberately not a fresh
        /// <c>SqlConnection</c> built from the connection string: Hangfire
        /// probes the database while services are still being registered, and
        /// on a first run that probe fails and puts SqlClient's pool into its
        /// failure-blocking period, so a new connection opened moments later
        /// fails fast without ever reaching the server. Reusing the connection
        /// that just migrated sidesteps the pool entirely.
        /// </param>
        public static void EnsureInstalled(DbConnection connection)
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            SqlServerObjectsInstaller.Install(connection);
        }
    }
}
