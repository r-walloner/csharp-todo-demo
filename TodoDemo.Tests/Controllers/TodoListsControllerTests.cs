using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TodoDemo.Contracts;
using TodoDemo.Controllers;
using TodoDemo.Database;
using TodoDemo.Entities;
using TodoDemo.Tests.Testing;

namespace TodoDemo.Tests.Controllers;

// AI-GENERATED: this test file was written by an AI coding assistant. Review before
// relying on it as a spec of intended behavior.
//
// NOTE: a bug originally found in this controller (GetTodoLists/GetTodoList never
// Include-ing Items, so ItemCount/OpenItemCount always reported 0) was fixed in the
// source before these tests were finalized. The tests below assert the
// corrected/current behavior directly rather than pinning a bug.
//
// Automatic [ApiController] model validation ([Required]/[MaxLength] on the request
// DTOs) is out of scope for these tests since it only runs through the real ASP.NET
// Core MVC pipeline, not when calling a controller action method directly.
public class TodoListsControllerTests
{
    private static TodoListsController CreateController(TodoDbContext db) =>
        new(db, NullLogger<TodoListsController>.Instance);

    private static TodoList NewList(string title = "List", DateTime? createdAt = null) => new()
    {
        Title = title,
        CreatedAt = createdAt ?? DateTime.UtcNow
    };

    // ---------- GetTodoLists ----------

    [Fact]
    public async Task GetTodoLists_NoListsInDb_ReturnsEmptyItemsAndZeroTotals()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);

        var result = await controller.GetTodoLists();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PagedResponse<TodoListResponse>>(ok.Value);
        Assert.Empty(response.Items);
        Assert.Equal(0, response.TotalCount);
        Assert.Equal(0, response.TotalPages);
    }

    [Fact]
    public async Task GetTodoLists_MultipleLists_OrdersByCreatedAtDescending()
    {
        using var db = TestDbContextFactory.Create();
        var oldest = NewList("Oldest", DateTime.UtcNow.AddDays(-2));
        var middle = NewList("Middle", DateTime.UtcNow.AddDays(-1));
        var newest = NewList("Newest", DateTime.UtcNow);
        db.TodoLists.AddRange(oldest, middle, newest);
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoLists();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PagedResponse<TodoListResponse>>(ok.Value);
        Assert.Equal(["Newest", "Middle", "Oldest"], response.Items.Select(i => i.Title));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetTodoLists_PageBelowMinimum_ClampsToPageOne(int page)
    {
        using var db = TestDbContextFactory.Create();

        var result = await CreateController(db).GetTodoLists(page: page);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PagedResponse<TodoListResponse>>(ok.Value);
        Assert.Equal(1, response.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task GetTodoLists_PageSizeBelowMinimum_ClampsToOne(int pageSize)
    {
        using var db = TestDbContextFactory.Create();

        var result = await CreateController(db).GetTodoLists(pageSize: pageSize);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PagedResponse<TodoListResponse>>(ok.Value);
        Assert.Equal(1, response.PageSize);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(1000)]
    public async Task GetTodoLists_PageSizeAboveMaximum_ClampsTo100(int pageSize)
    {
        using var db = TestDbContextFactory.Create();

        var result = await CreateController(db).GetTodoLists(pageSize: pageSize);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PagedResponse<TodoListResponse>>(ok.Value);
        Assert.Equal(100, response.PageSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task GetTodoLists_PageSizeAtExactBoundaries_PassesThroughUnchanged(int pageSize)
    {
        using var db = TestDbContextFactory.Create();

        var result = await CreateController(db).GetTodoLists(pageSize: pageSize);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PagedResponse<TodoListResponse>>(ok.Value);
        Assert.Equal(pageSize, response.PageSize);
    }

    [Fact]
    public async Task GetTodoLists_PaginationAcrossMultiplePages_ReturnsCorrectSlicesAndTotals()
    {
        using var db = TestDbContextFactory.Create();
        for (var i = 0; i < 25; i++)
        {
            db.TodoLists.Add(NewList($"List {i:D2}", DateTime.UtcNow.AddMinutes(-i)));
        }
        await db.SaveChangesAsync();

        var page1 = await CreateController(db).GetTodoLists(page: 1, pageSize: 10);
        var page1Response = Assert.IsType<PagedResponse<TodoListResponse>>(
            Assert.IsType<OkObjectResult>(page1.Result).Value);
        Assert.Equal(10, page1Response.Items.Count);
        Assert.Equal(25, page1Response.TotalCount);
        Assert.Equal(3, page1Response.TotalPages);

        var page3 = await CreateController(db).GetTodoLists(page: 3, pageSize: 10);
        var page3Response = Assert.IsType<PagedResponse<TodoListResponse>>(
            Assert.IsType<OkObjectResult>(page3.Result).Value);
        Assert.Equal(5, page3Response.Items.Count);
    }

    [Fact]
    public async Task GetTodoLists_PageBeyondAvailableData_ReturnsEmptyItemsButCorrectTotals()
    {
        using var db = TestDbContextFactory.Create();
        for (var i = 0; i < 25; i++)
        {
            db.TodoLists.Add(NewList($"List {i:D2}", DateTime.UtcNow.AddMinutes(-i)));
        }
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoLists(page: 4, pageSize: 10);

        var response = Assert.IsType<PagedResponse<TodoListResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Empty(response.Items);
        Assert.Equal(25, response.TotalCount);
        Assert.Equal(3, response.TotalPages);
    }

    // ---------- GetTodoList ----------

    [Fact]
    public async Task GetTodoList_ExistingListWithNoItems_ReturnsOkWithMappedFields()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList("My List");
        list.Description = "Some description";
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoList(list.Id);

        var response = Assert.IsType<TodoListResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(list.Id, response.Id);
        Assert.Equal(list.Title, response.Title);
        Assert.Equal(list.Description, response.Description);
        Assert.Equal(0, response.ItemCount);
        Assert.Equal(0, response.OpenItemCount);
        Assert.Equal(list.CreatedAt, response.CreatedAt);
        Assert.Equal(list.UpdatedAt, response.UpdatedAt);
    }

    [Fact]
    public async Task GetTodoList_NonExistentId_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();

        var result = await CreateController(db).GetTodoList(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetTodoList_ReportsItemCountAndOpenItemCount_EvenFromAFreshDbContext()
    {
        var dbName = Guid.NewGuid().ToString();
        using var seedDb = TestDbContextFactory.Create(dbName);
        var list = NewList("List with items");
        seedDb.TodoLists.Add(list);
        seedDb.TodoItems.Add(new TodoItem { Title = "Item 1", TodoListId = list.Id, IsCompleted = true });
        seedDb.TodoItems.Add(new TodoItem { Title = "Item 2", TodoListId = list.Id, IsCompleted = false });
        await seedDb.SaveChangesAsync();

        // A second, independent DbContext instance against the same underlying store —
        // its change tracker has nothing pre-loaded, so ItemCount/OpenItemCount can only
        // be correct here if the controller actually re-queries (Includes) the items
        // rather than relying on whatever happens to already be tracked.
        using var freshDb = TestDbContextFactory.Create(dbName);
        var itemCountInStore = await freshDb.TodoItems.CountAsync(i => i.TodoListId == list.Id);
        Assert.Equal(2, itemCountInStore); // sanity check: the items really exist

        var result = await CreateController(freshDb).GetTodoList(list.Id);

        var response = Assert.IsType<TodoListResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, response.ItemCount);
        Assert.Equal(1, response.OpenItemCount);
    }

    // ---------- CreateTodoList ----------

    [Theory]
    [InlineData(null, "")]
    [InlineData("hello", "hello")]
    [InlineData("", "")]
    public async Task CreateTodoList_Description_NullOmittedOrProvided(string? requestDescription, string expectedDescription)
    {
        using var db = TestDbContextFactory.Create();
        var request = new CreateTodoListRequest { Title = "Title", Description = requestDescription };

        var result = await CreateController(db).CreateTodoList(request);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<TodoListResponse>(created.Value);
        Assert.Equal(expectedDescription, response.Description);
    }

    [Fact]
    public async Task CreateTodoList_ReturnsCreatedAtActionResult_WithCorrectRouteValuesAndBody()
    {
        using var db = TestDbContextFactory.Create();
        var request = new CreateTodoListRequest { Title = "New List", Description = "Desc" };

        var result = await CreateController(db).CreateTodoList(request);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<TodoListResponse>(created.Value);
        Assert.Equal(nameof(TodoListsController.GetTodoList), created.ActionName);
        Assert.Equal(response.Id, created.RouteValues!["id"]);
        Assert.Equal("New List", response.Title);
        Assert.Equal("Desc", response.Description);

        var persisted = await db.TodoLists.FindAsync(response.Id);
        Assert.NotNull(persisted);
        Assert.Equal("New List", persisted!.Title);
    }

    // ---------- UpdateTodoList ----------

    [Fact]
    public async Task UpdateTodoList_TitleOnlyProvided_UpdatesTitleLeavesDescriptionUnchanged()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList("Old Title");
        list.Description = "Original Description";
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdateTodoList(list.Id, new UpdateTodoListRequest { Title = "New Title" });

        Assert.IsType<NoContentResult>(result);
        var updated = await db.TodoLists.FindAsync(list.Id);
        Assert.Equal("New Title", updated!.Title);
        Assert.Equal("Original Description", updated.Description);
    }

    [Fact]
    public async Task UpdateTodoList_DescriptionOnlyProvided_UpdatesDescriptionLeavesTitleUnchanged()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList("Stable Title");
        list.Description = "Old Description";
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdateTodoList(list.Id, new UpdateTodoListRequest { Description = "New Description" });

        Assert.IsType<NoContentResult>(result);
        var updated = await db.TodoLists.FindAsync(list.Id);
        Assert.Equal("Stable Title", updated!.Title);
        Assert.Equal("New Description", updated.Description);
    }

    [Fact]
    public async Task UpdateTodoList_BothFieldsNull_LeavesValuesButStillBumpsUpdatedAt()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList("Title");
        list.Description = "Description";
        list.UpdatedAt = DateTime.UtcNow.AddDays(-1);
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();
        var updatedAtBefore = list.UpdatedAt;

        var result = await CreateController(db).UpdateTodoList(list.Id, new UpdateTodoListRequest());

        Assert.IsType<NoContentResult>(result);
        var updated = await db.TodoLists.FindAsync(list.Id);
        Assert.Equal("Title", updated!.Title);
        Assert.Equal("Description", updated.Description);
        // NOTE: not a bug — UpdateTodoList unconditionally bumps UpdatedAt to DateTime.UtcNow
        // even when both Title and Description are omitted (i.e. a fully no-op update still
        // changes UpdatedAt). This test documents that intentional-looking behavior.
        Assert.True(updated.UpdatedAt > updatedAtBefore);
    }

    [Fact]
    public async Task UpdateTodoList_NonExistentId_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();

        var result = await CreateController(db).UpdateTodoList(Guid.NewGuid(), new UpdateTodoListRequest { Title = "x" });

        Assert.IsType<NotFoundResult>(result);
    }

    // ---------- DeleteTodoList ----------

    [Fact]
    public async Task DeleteTodoList_ExistingList_RemovesItAndReturnsNoContent()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList("To Delete");
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();

        var result = await CreateController(db).DeleteTodoList(list.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await db.TodoLists.FindAsync(list.Id));
    }

    [Fact]
    public async Task DeleteTodoList_NonExistentId_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();

        var result = await CreateController(db).DeleteTodoList(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteTodoList_ChildItemsSurviveInInMemoryProvider_DoesNotReflectRealPostgresCascade()
    {
        var dbName = Guid.NewGuid().ToString();
        using var seedDb = TestDbContextFactory.Create(dbName);
        var list = NewList("Parent");
        seedDb.TodoLists.Add(list);
        seedDb.TodoItems.Add(new TodoItem { Title = "Child 1", TodoListId = list.Id });
        seedDb.TodoItems.Add(new TodoItem { Title = "Child 2", TodoListId = list.Id });
        await seedDb.SaveChangesAsync();

        // A fresh context: DeleteTodoList never loads Items, so EF's cascade-delete logic
        // (which operates on the tracked entity graph) has nothing to cascade to here.
        using var freshDb = TestDbContextFactory.Create(dbName);
        var result = await CreateController(freshDb).DeleteTodoList(list.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, await freshDb.TodoLists.CountAsync());
        // NOTE: this is a real behavior of the current C# code combined with the InMemory
        // provider — it does NOT represent the real Postgres deployment, where
        // ON DELETE CASCADE is enforced by the database engine on the DELETE statement
        // itself, independent of what the app has loaded into memory, and WOULD remove
        // these child rows. Do not read this assertion as "cascade delete doesn't work."
        Assert.Equal(2, await freshDb.TodoItems.CountAsync(i => i.TodoListId == list.Id));
    }
}
