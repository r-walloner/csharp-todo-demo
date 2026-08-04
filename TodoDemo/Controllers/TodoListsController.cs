using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoDemo.Contracts;
using TodoDemo.Database;
using TodoDemo.Entities;

namespace TodoDemo.Controllers;

[ApiController]
[Route("api/todo-lists")]
[Produces("application/json")]
public class TodoListsController(TodoDbContext db, ILogger<TodoListsController> logger) : ControllerBase
{
    private const int MaxPageSize = 100;

    // GET /api/todo-lists?page=1&pageSize=10
    [HttpGet]
    [ProducesResponseType<PagedResponse<TodoListResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<TodoListResponse>>> GetTodoLists(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default
    )
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var totalCount = await db.TodoLists.CountAsync(cancellationToken);

        var items = await db.TodoLists
            .Include(l => l.Items)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var response = new PagedResponse<TodoListResponse>(
            items.Select(ToResponse).ToList(),
            page,
            pageSize,
            totalCount
        );

        return Ok(response);
    }

    // GET /api/todo-lists/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TodoListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TodoListResponse>> GetTodoList(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var todoList = await db.TodoLists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (todoList is null)
        {
            return NotFound();
        }

        var response = ToResponse(todoList);

        return Ok(response);
    }

    // POST /api/todo-lists
    [HttpPost]
    [ProducesResponseType<TodoListResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TodoListResponse>> CreateTodoList(
        [FromBody] CreateTodoListRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var todoList = new TodoList
        {
            Title = request.Title,
            Description = request.Description ?? string.Empty
        };

        db.TodoLists.Add(todoList);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Created new todo list named '{Title}' with ID {Id}",
            todoList.Title, todoList.Id);

        var response = ToResponse(todoList);

        return CreatedAtAction(nameof(GetTodoList), new { id = todoList.Id }, response);
    }

    // PUT /api/todo-lists/{id}
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateTodoList(
        [FromRoute] Guid id,
        [FromBody] UpdateTodoListRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var todoList = await db.TodoLists
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (todoList is null)
        {
            return NotFound();
        }

        if (request.Title is not null && !request.Title.IsWhiteSpace())
            todoList.Title = request.Title;
        if (request.Description is not null)
            todoList.Description = request.Description;

        todoList.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Updated todo list with ID {Id}", todoList.Id);

        return NoContent();
    }

    // DELETE /api/todo-lists/{id}
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteTodoList(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var todoList = await db.TodoLists
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (todoList is null)
        {
            return NotFound();
        }

        db.TodoLists.Remove(todoList);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Deleted todo list with ID {Id}", todoList.Id);

        return NoContent();
    }

    /// <summary>
    /// Maps a TodoList entity to a TodoListResponse
    /// </summary>
    /// <param name="todoList">The TodoList entity to map</param>
    /// <returns>A TodoListResponse representing the TodoList entity</returns>
    private TodoListResponse ToResponse(TodoList todoList) =>
        new TodoListResponse(
            todoList.Id,
            todoList.Title,
            todoList.Description,
            todoList.Items.Count,
            todoList.Items.Count(i => !i.IsCompleted),
            todoList.CreatedAt,
            todoList.UpdatedAt
        );
}