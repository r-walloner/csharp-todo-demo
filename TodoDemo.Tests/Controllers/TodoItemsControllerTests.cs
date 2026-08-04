using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TodoDemo.Contracts;
using TodoDemo.Controllers;
using TodoDemo.Database;
using TodoDemo.Entities;
using TodoDemo.Entities.Enums;
using TodoDemo.Tests.Testing;

namespace TodoDemo.Tests.Controllers;

// AI-GENERATED: this test file was written by an AI coding assistant. Review before
// relying on it as a spec of intended behavior.
//
// NOTE: two bugs originally found in this controller (CreateTodoItem never assigning
// TodoListId to the new item, and GetTodoItem/UpdateTodoItem/DeleteTodoItem not
// filtering by TodoListId) were fixed in the source before these tests were finalized.
// The tests below assert the corrected/current behavior directly rather than pinning
// a bug. See TodoListsControllerTests.cs for BUG C, which is still present.
//
// Automatic [ApiController] model validation ([Required]/[MaxLength]/[EnumDataType]
// on the request DTOs) is out of scope for these tests since it only runs through the
// real ASP.NET Core MVC pipeline, not when calling a controller action method directly.
//
// Also out of scope: distinguishing "field omitted in the JSON body" from "field
// explicitly set to null" for optional nullable request fields — both collapse to the
// same C# null on the deserialized request object once you're calling the action
// directly, so there's no test for "explicitly clear DueAt via update" (the controller
// provides no code path for that anyway: `if (request.DueAt is not null)` never assigns
// null).
public class TodoItemsControllerTests
{
    private static TodoItemsController CreateController(TodoDbContext db) =>
        new(db, NullLogger<TodoItemsController>.Instance);

    private static TodoList NewList(string title = "List") => new() { Title = title };

    private static TodoItem NewItem(Guid listId, string title = "Item", DateTime? createdAt = null) => new()
    {
        Title = title,
        TodoListId = listId,
        CreatedAt = createdAt ?? DateTime.UtcNow
    };

    // ---------- GetTodoItems ----------

    [Fact]
    public async Task GetTodoItems_ListDoesNotExist_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();

        var result = await CreateController(db).GetTodoItems(Guid.NewGuid(), isCompleted: null);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetTodoItems_NoItemsInList_ReturnsEmptyPagedResponse()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoItems(list.Id, isCompleted: null);

        var response = Assert.IsType<PagedResponse<TodoItemResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Empty(response.Items);
        Assert.Equal(0, response.TotalCount);
    }

    [Fact]
    public async Task GetTodoItems_IsCompletedFilterTrue_ReturnsOnlyCompletedItemsAndCorrectTotalCount()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        db.TodoItems.Add(new TodoItem { Title = "Done 1", TodoListId = list.Id, IsCompleted = true });
        db.TodoItems.Add(new TodoItem { Title = "Done 2", TodoListId = list.Id, IsCompleted = true });
        db.TodoItems.Add(new TodoItem { Title = "Open", TodoListId = list.Id, IsCompleted = false });
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoItems(list.Id, isCompleted: true);

        var response = Assert.IsType<PagedResponse<TodoItemResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, response.TotalCount);
        Assert.All(response.Items, i => Assert.True(i.IsCompleted));
    }

    [Fact]
    public async Task GetTodoItems_IsCompletedFilterFalse_ReturnsOnlyIncompleteItemsAndCorrectTotalCount()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        db.TodoItems.Add(new TodoItem { Title = "Done", TodoListId = list.Id, IsCompleted = true });
        db.TodoItems.Add(new TodoItem { Title = "Open 1", TodoListId = list.Id, IsCompleted = false });
        db.TodoItems.Add(new TodoItem { Title = "Open 2", TodoListId = list.Id, IsCompleted = false });
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoItems(list.Id, isCompleted: false);

        var response = Assert.IsType<PagedResponse<TodoItemResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, response.TotalCount);
        Assert.All(response.Items, i => Assert.False(i.IsCompleted));
    }

    [Fact]
    public async Task GetTodoItems_IsCompletedFilterOmitted_ReturnsAllItemsRegardlessOfCompletion()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        db.TodoItems.Add(new TodoItem { Title = "Done", TodoListId = list.Id, IsCompleted = true });
        db.TodoItems.Add(new TodoItem { Title = "Open", TodoListId = list.Id, IsCompleted = false });
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoItems(list.Id, isCompleted: null);

        var response = Assert.IsType<PagedResponse<TodoItemResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, response.TotalCount);
    }

    [Fact]
    public async Task GetTodoItems_MultipleItems_OrdersByCreatedAtDescending()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        db.TodoItems.Add(NewItem(list.Id, "Oldest", DateTime.UtcNow.AddDays(-2)));
        db.TodoItems.Add(NewItem(list.Id, "Middle", DateTime.UtcNow.AddDays(-1)));
        db.TodoItems.Add(NewItem(list.Id, "Newest", DateTime.UtcNow));
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoItems(list.Id, isCompleted: null);

        var response = Assert.IsType<PagedResponse<TodoItemResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(["Newest", "Middle", "Oldest"], response.Items.Select(i => i.Title));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task GetTodoItems_PageBelowMinimum_ClampsToPageOne(int page)
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoItems(list.Id, isCompleted: null, page: page);

        var response = Assert.IsType<PagedResponse<TodoItemResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(1, response.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task GetTodoItems_PageSizeBelowMinimum_ClampsToOne(int pageSize)
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoItems(list.Id, isCompleted: null, pageSize: pageSize);

        var response = Assert.IsType<PagedResponse<TodoItemResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(1, response.PageSize);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(1000)]
    public async Task GetTodoItems_PageSizeAboveMaximum_ClampsTo100(int pageSize)
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoItems(list.Id, isCompleted: null, pageSize: pageSize);

        var response = Assert.IsType<PagedResponse<TodoItemResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(100, response.PageSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task GetTodoItems_PageSizeAtExactBoundaries_PassesThroughUnchanged(int pageSize)
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoItems(list.Id, isCompleted: null, pageSize: pageSize);

        var response = Assert.IsType<PagedResponse<TodoItemResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(pageSize, response.PageSize);
    }

    [Fact]
    public async Task GetTodoItems_PaginationAcrossMultiplePages_ReturnsCorrectSlicesAndTotals()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        for (var i = 0; i < 25; i++)
        {
            db.TodoItems.Add(NewItem(list.Id, $"Item {i:D2}", DateTime.UtcNow.AddMinutes(-i)));
        }
        await db.SaveChangesAsync();

        var page1 = await CreateController(db).GetTodoItems(list.Id, isCompleted: null, page: 1, pageSize: 10);
        var page1Response = Assert.IsType<PagedResponse<TodoItemResponse>>(
            Assert.IsType<OkObjectResult>(page1.Result).Value);
        Assert.Equal(10, page1Response.Items.Count);
        Assert.Equal(25, page1Response.TotalCount);
        Assert.Equal(3, page1Response.TotalPages);

        var page3 = await CreateController(db).GetTodoItems(list.Id, isCompleted: null, page: 3, pageSize: 10);
        var page3Response = Assert.IsType<PagedResponse<TodoItemResponse>>(
            Assert.IsType<OkObjectResult>(page3.Result).Value);
        Assert.Equal(5, page3Response.Items.Count);
    }

    // ---------- GetTodoItem ----------

    [Fact]
    public async Task GetTodoItem_ExistingItemInCorrectList_ReturnsOkWithMappedFields()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        var item = NewItem(list.Id, "My Item");
        item.Notes = "Some notes";
        item.Priority = TodoPriority.High;
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoItem(list.Id, item.Id);

        var response = Assert.IsType<TodoItemResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(item.Id, response.Id);
        Assert.Equal(list.Id, response.TodoListId);
        Assert.Equal("My Item", response.Title);
        Assert.Equal("Some notes", response.Notes);
        Assert.Equal(nameof(TodoPriority.High), response.Priority);
    }

    [Fact]
    public async Task GetTodoItem_ListDoesNotExist_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();

        var result = await CreateController(db).GetTodoItem(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetTodoItem_ItemDoesNotExist_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoItem(list.Id, Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- CreateTodoItem ----------

    [Fact]
    public async Task CreateTodoItem_ListDoesNotExist_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var request = new CreateTodoItemRequest { Title = "x" };

        var result = await CreateController(db).CreateTodoItem(Guid.NewGuid(), request);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("n", "n")]
    [InlineData("", "")]
    public async Task CreateTodoItem_Notes_NullOmittedOrProvided(string? requestNotes, string expectedNotes)
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();
        var request = new CreateTodoItemRequest { Title = "x", Notes = requestNotes };

        var result = await CreateController(db).CreateTodoItem(list.Id, request);

        var response = Assert.IsType<TodoItemResponse>(
            Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(expectedNotes, response.Notes);
    }

    [Theory]
    [InlineData("High", TodoPriority.High)]
    [InlineData("low", TodoPriority.Low)]
    [InlineData("VERYHIGH", TodoPriority.VeryHigh)]
    public async Task CreateTodoItem_PriorityValidString_ParsesCaseInsensitively(string priority, TodoPriority expected)
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();
        var request = new CreateTodoItemRequest { Title = "x", Priority = priority };

        var result = await CreateController(db).CreateTodoItem(list.Id, request);

        var response = Assert.IsType<TodoItemResponse>(
            Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(expected.ToString(), response.Priority);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NotARealPriority")]
    public async Task CreateTodoItem_PriorityInvalidOrMissingString_FallsBackToMedium(string? priority)
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();
        var request = new CreateTodoItemRequest { Title = "x", Priority = priority };

        var result = await CreateController(db).CreateTodoItem(list.Id, request);

        var response = Assert.IsType<TodoItemResponse>(
            Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(nameof(TodoPriority.Medium), response.Priority);
    }

    [Fact]
    public async Task CreateTodoItem_DueAtProvided_IsPersisted()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();
        var dueAt = DateTime.UtcNow.AddDays(3);
        var request = new CreateTodoItemRequest { Title = "x", DueAt = dueAt };

        var result = await CreateController(db).CreateTodoItem(list.Id, request);

        var response = Assert.IsType<TodoItemResponse>(
            Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(dueAt, response.DueAt);
    }

    [Fact]
    public async Task CreateTodoItem_DueAtOmitted_RemainsNull()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();
        var request = new CreateTodoItemRequest { Title = "x" };

        var result = await CreateController(db).CreateTodoItem(list.Id, request);

        var response = Assert.IsType<TodoItemResponse>(
            Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Null(response.DueAt);
    }

    [Fact]
    public async Task CreateTodoItem_ReturnsCreatedAtActionResult_WithMappedResponseBody()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();
        var request = new CreateTodoItemRequest { Title = "New Item" };

        var result = await CreateController(db).CreateTodoItem(list.Id, request);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<TodoItemResponse>(created.Value);
        Assert.Equal(nameof(TodoItemsController.GetTodoItem), created.ActionName);
        Assert.Equal("New Item", response.Title);
        Assert.False(response.IsCompleted);
    }

    [Fact]
    public async Task CreateTodoItem_SetsTodoListIdFromRoute()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();
        var request = new CreateTodoItemRequest { Title = "New Item" };

        var result = await CreateController(db).CreateTodoItem(list.Id, request);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<TodoItemResponse>(created.Value);
        Assert.Equal(list.Id, response.TodoListId);
        Assert.Equal(list.Id, created.RouteValues!["listId"]);

        var persisted = await db.TodoItems.FindAsync(response.Id);
        Assert.Equal(list.Id, persisted!.TodoListId);

        // The item is correctly visible via the collection endpoint for its real parent list.
        var itemsForList = await CreateController(db).GetTodoItems(list.Id, isCompleted: null);
        var itemsResponse = Assert.IsType<PagedResponse<TodoItemResponse>>(
            Assert.IsType<OkObjectResult>(itemsForList.Result).Value);
        Assert.Single(itemsResponse.Items);
    }

    // ---------- UpdateTodoItem ----------

    [Fact]
    public async Task UpdateTodoItem_TitleOnlyProvided_UpdatesTitleLeavesOthersUnchanged()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        var item = NewItem(list.Id, "Old Title");
        item.Notes = "Notes";
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdateTodoItem(list.Id, item.Id, new UpdateTodoItemRequest { Title = "New Title" });

        Assert.IsType<NoContentResult>(result);
        var updated = await db.TodoItems.FindAsync(item.Id);
        Assert.Equal("New Title", updated!.Title);
        Assert.Equal("Notes", updated.Notes);
    }

    [Fact]
    public async Task UpdateTodoItem_NotesOnlyProvided_UpdatesNotesLeavesOthersUnchanged()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        var item = NewItem(list.Id, "Title");
        item.Notes = "Old Notes";
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdateTodoItem(list.Id, item.Id, new UpdateTodoItemRequest { Notes = "New Notes" });

        Assert.IsType<NoContentResult>(result);
        var updated = await db.TodoItems.FindAsync(item.Id);
        Assert.Equal("Title", updated!.Title);
        Assert.Equal("New Notes", updated.Notes);
    }

    [Fact]
    public async Task UpdateTodoItem_PriorityValidString_UpdatesPriority()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        var item = NewItem(list.Id);
        item.Priority = TodoPriority.Low;
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdateTodoItem(list.Id, item.Id, new UpdateTodoItemRequest { Priority = "medium" });

        Assert.IsType<NoContentResult>(result);
        var updated = await db.TodoItems.FindAsync(item.Id);
        Assert.Equal(TodoPriority.Medium, updated!.Priority);
    }

    [Fact]
    public async Task UpdateTodoItem_PriorityInvalidString_LeavesExistingPriorityUnchanged()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        var item = NewItem(list.Id);
        item.Priority = TodoPriority.High;
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdateTodoItem(list.Id, item.Id, new UpdateTodoItemRequest { Priority = "NotReal" });

        Assert.IsType<NoContentResult>(result);
        // NOTE: not a bug, but an asymmetry worth documenting — unlike CreateTodoItem
        // (which falls back to Medium on an unparsable Priority string), UpdateTodoItem
        // simply skips the assignment entirely on a parse failure, leaving the existing
        // value untouched.
        var updated = await db.TodoItems.FindAsync(item.Id);
        Assert.Equal(TodoPriority.High, updated!.Priority);
    }

    [Fact]
    public async Task UpdateTodoItem_IsCompletedSetTrue_SetsCompletedAtToUtcNow()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        var item = NewItem(list.Id);
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdateTodoItem(list.Id, item.Id, new UpdateTodoItemRequest { IsCompleted = true });

        Assert.IsType<NoContentResult>(result);
        var updated = await db.TodoItems.FindAsync(item.Id);
        Assert.True(updated!.IsCompleted);
        Assert.NotNull(updated.CompletedAt);
    }

    [Fact]
    public async Task UpdateTodoItem_IsCompletedSetFalse_ClearsCompletedAt()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        var item = NewItem(list.Id);
        item.IsCompleted = true;
        item.CompletedAt = DateTime.UtcNow.AddDays(-1);
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdateTodoItem(list.Id, item.Id, new UpdateTodoItemRequest { IsCompleted = false });

        Assert.IsType<NoContentResult>(result);
        var updated = await db.TodoItems.FindAsync(item.Id);
        Assert.False(updated!.IsCompleted);
        Assert.Null(updated.CompletedAt);
    }

    [Fact]
    public async Task UpdateTodoItem_DueAtProvided_UpdatesDueAt()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        var item = NewItem(list.Id);
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();
        var newDueAt = DateTime.UtcNow.AddDays(5);

        var result = await CreateController(db).UpdateTodoItem(list.Id, item.Id, new UpdateTodoItemRequest { DueAt = newDueAt });

        Assert.IsType<NoContentResult>(result);
        var updated = await db.TodoItems.FindAsync(item.Id);
        Assert.Equal(newDueAt, updated!.DueAt);
    }

    [Fact]
    public async Task UpdateTodoItem_AllFieldsNull_LeavesEverythingUnchanged()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        var item = NewItem(list.Id, "Title");
        item.Notes = "Notes";
        item.Priority = TodoPriority.High;
        var dueAt = DateTime.UtcNow.AddDays(1);
        item.DueAt = dueAt;
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdateTodoItem(list.Id, item.Id, new UpdateTodoItemRequest());

        Assert.IsType<NoContentResult>(result);
        var updated = await db.TodoItems.FindAsync(item.Id);
        Assert.Equal("Title", updated!.Title);
        Assert.Equal("Notes", updated.Notes);
        Assert.Equal(TodoPriority.High, updated.Priority);
        Assert.False(updated.IsCompleted);
        Assert.Equal(dueAt, updated.DueAt);
    }

    [Fact]
    public async Task UpdateTodoItem_ListDoesNotExist_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();

        var result = await CreateController(db).UpdateTodoItem(Guid.NewGuid(), Guid.NewGuid(), new UpdateTodoItemRequest { Title = "x" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateTodoItem_ItemDoesNotExist_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdateTodoItem(list.Id, Guid.NewGuid(), new UpdateTodoItemRequest { Title = "x" });

        Assert.IsType<NotFoundResult>(result);
    }

    // ---------- DeleteTodoItem ----------

    [Fact]
    public async Task DeleteTodoItem_ExistingItem_RemovesItAndReturnsNoContent()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        var item = NewItem(list.Id);
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();

        var result = await CreateController(db).DeleteTodoItem(list.Id, item.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await db.TodoItems.FindAsync(item.Id));
    }

    [Fact]
    public async Task DeleteTodoItem_ListDoesNotExist_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();

        var result = await CreateController(db).DeleteTodoItem(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteTodoItem_ItemDoesNotExist_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var list = NewList();
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();

        var result = await CreateController(db).DeleteTodoItem(list.Id, Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    // ---------- Cross-list access to a single item is correctly rejected ----------

    [Fact]
    public async Task GetTodoItem_ItemBelongsToADifferentList_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var listA = NewList("List A");
        var listB = NewList("List B");
        db.TodoLists.AddRange(listA, listB);
        var item = NewItem(listA.Id, "Belongs to A");
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTodoItem(listB.Id, item.Id);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateTodoItem_ItemBelongsToADifferentList_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var listA = NewList("List A");
        var listB = NewList("List B");
        db.TodoLists.AddRange(listA, listB);
        var item = NewItem(listA.Id, "Belongs to A");
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdateTodoItem(listB.Id, item.Id, new UpdateTodoItemRequest { Title = "Changed via B" });

        Assert.IsType<NotFoundResult>(result);
        var unchanged = await db.TodoItems.FindAsync(item.Id);
        Assert.Equal("Belongs to A", unchanged!.Title);
    }

    [Fact]
    public async Task DeleteTodoItem_ItemBelongsToADifferentList_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var listA = NewList("List A");
        var listB = NewList("List B");
        db.TodoLists.AddRange(listA, listB);
        var item = NewItem(listA.Id, "Belongs to A");
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();

        var result = await CreateController(db).DeleteTodoItem(listB.Id, item.Id);

        Assert.IsType<NotFoundResult>(result);
        Assert.NotNull(await db.TodoItems.FindAsync(item.Id));
    }
}
