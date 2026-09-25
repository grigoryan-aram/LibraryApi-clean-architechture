using LibraryApi.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.UnitTests.Repositories;

/// <summary>
/// A real relational database, in memory, for one test.
/// </summary>
/// <remarks>
/// SQLite rather than the EF Core InMemory provider for two reasons, both of
/// which have cost time here before: every repository deletes with
/// <c>ExecuteDeleteAsync</c>, which is relational-only and throws outright on
/// InMemory; and InMemory enforces no constraints at all, so it can prove
/// nothing about foreign keys, unique indexes or check constraints.
///
/// The connection is held open for the fixture's lifetime on purpose — a
/// <c>DataSource=:memory:</c> database is destroyed the moment its last
/// connection closes.
/// </remarks>
public sealed class SqliteDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public LibraryDBContext Context { get; }

    public SqliteDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Context = new LibraryDBContext(
            new DbContextOptionsBuilder<LibraryDBContext>()
                .UseSqlite(_connection)
                .Options);

        Context.Database.EnsureCreated();

        // EnsureCreated applies the HasData seed — 15 books and 11 categories.
        // Tests assert on counts and ordering, so they start from a known
        // empty table and insert exactly what they mean to.
        Context.Books.RemoveRange(Context.Books);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    /// <summary>
    /// A second context over the same database, so a test can read back what
    /// the repository wrote without the change tracker answering from memory.
    /// </summary>
    public LibraryDBContext NewContext() =>
        new(new DbContextOptionsBuilder<LibraryDBContext>()
            .UseSqlite(_connection)
            .Options);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
