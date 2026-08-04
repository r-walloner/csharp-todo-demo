using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoDemo.Contracts;
using TodoDemo.Database;
using TodoDemo.Entities;
using TodoDemo.Entities.Enums;

namespace TodoDemo.Controllers;

[ApiController]
[Route("api/todo-lists/{listId:guid}/items")]
[Produces("application/json")]
public class TodoItemsController(TodoDbContext db, ILogger<TodoItemsController> logger) : ControllerBase
{
    private const int MaxPageSize = 100;

    // GET /api/todo-lists/{listId}/items[?isCompleted=false&page=1&pageSize=10]
    [HttpGet]
    [ProducesResponseType<PagedResponse<TodoItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<TodoItemResponse>>> GetTodoItems(
        [FromRoute] Guid listId,
        [FromQuery] bool? isCompleted,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default
    )
    {
        if (!await ListExistsAsync(listId, cancellationToken))
            return NotFound();

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.TodoItems.Where(i => i.TodoListId == listId);

        if (isCompleted is not null)
            query = query.Where(i => i.IsCompleted == isCompleted.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<TodoItemResponse>(
            ToResponse(items),
            page,
            pageSize,
            totalCount
        ));
    }

    // GET /api/todo-lists/{listId}/items/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TodoItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TodoItemResponse>> GetTodoItem(
        [FromRoute] Guid listId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default
    )
    {
        if (!await ListExistsAsync(listId, cancellationToken))
            return NotFound();

        var item = await db.TodoItems.FirstOrDefaultAsync(
            i => i.Id == id && i.TodoListId == listId, cancellationToken);

        if (item is null)
            return NotFound();

        return Ok(ToResponse(item));
    }

    // POST /api/todo-lists/{listId}/items
    [HttpPost]
    [ProducesResponseType<TodoItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TodoItemResponse>> CreateTodoItem(
        [FromRoute] Guid listId,
        [FromBody] CreateTodoItemRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (!await ListExistsAsync(listId, cancellationToken))
            return NotFound();

        var item = new TodoItem
        {
            TodoListId = listId,
            Title = request.Title,
            Notes = request.Notes ?? string.Empty,
            Priority = Enum.TryParse<TodoPriority>(request.Priority, ignoreCase: true, out var p)
                ? p : TodoPriority.Medium,
            DueAt = request.DueAt
        };

        db.TodoItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetTodoItem),
            new { listId = item.TodoListId, id = item.Id },
            ToResponse(item)
        );
    }

    // PUT /api/todo-lists/{listId}/items/{id}
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateTodoItem(
        [FromRoute] Guid listId,
        [FromRoute] Guid id,
        [FromBody] UpdateTodoItemRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (!await ListExistsAsync(listId, cancellationToken))
            return NotFound();

        var item = await db.TodoItems.FirstOrDefaultAsync(
            i => i.Id == id && i.TodoListId == listId, cancellationToken);
        if (item is null)
            return NotFound();

        if (request.Title is not null)
            item.Title = request.Title;
        if (request.Notes is not null)
            item.Notes = request.Notes;
        if (request.Priority is not null &&
            Enum.TryParse<TodoPriority>(request.Priority, ignoreCase: true, out var p))
            item.Priority = p;
        if (request.IsCompleted is not null)
        {
            item.IsCompleted = request.IsCompleted.Value;
            item.CompletedAt = request.IsCompleted.Value ? DateTime.UtcNow : null;
        }
        if (request.DueAt is not null)
            item.DueAt = request.DueAt;

        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    // DELETE /api/todo-lists/{listId}/items/{id}
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteTodoItem(
        [FromRoute] Guid listId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default
    )
    {
        if (!await ListExistsAsync(listId, cancellationToken))
            return NotFound();

        var item = await db.TodoItems.FirstOrDefaultAsync(
            i => i.Id == id && i.TodoListId == listId, cancellationToken);
        if (item is null)
            return NotFound();

        db.TodoItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Checks if a todo list with the given ID exists in the database.
    /// </summary>
    /// <param name="listId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private Task<bool> ListExistsAsync(Guid listId, CancellationToken cancellationToken) =>
        db.TodoLists.AnyAsync(l => l.Id == listId, cancellationToken);

    /// <summary>
    /// Maps a TodoItem entity to a TodoItemResponse response.
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    private TodoItemResponse ToResponse(TodoItem item) =>
        new TodoItemResponse(
            item.Id,
            item.TodoListId,
            item.Title,
            item.Notes,
            item.Priority.ToString(),
            item.IsCompleted,
            item.CreatedAt,
            item.CompletedAt,
            item.DueAt
        );

    /// <summary>
    /// Mapss a list of TodoItem entities to a list of TodoItemResponse responses.
    /// </summary>
    /// <param name="items"></param>
    /// <returns></returns>
    private List<TodoItemResponse> ToResponse(IReadOnlyList<TodoItem> items) =>
        items.Select(ToResponse).ToList();
}