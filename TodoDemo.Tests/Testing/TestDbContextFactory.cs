using Microsoft.EntityFrameworkCore;
using TodoDemo.Database;

namespace TodoDemo.Tests.Testing;

// AI-GENERATED: this test helper was written by an AI coding assistant. Review before
// relying on it as a spec of intended behavior.
/// <summary>
/// Creates TodoDbContext instances backed by EF Core's InMemory provider.
/// Call with no argument for a brand-new, fully isolated database (the default
/// for almost every test). Pass an explicit databaseName to open a SECOND,
/// independent DbContext instance (its own change tracker, no shared tracked
/// entities) against the SAME underlying store — needed to prove bugs where a
/// navigation collection only gets populated via change-tracker fixup between
/// entities already tracked by the same context instance, not by a real query.
/// </summary>
internal static class TestDbContextFactory
{
    public static TodoDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<TodoDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new TodoDbContext(options);
    }
}
