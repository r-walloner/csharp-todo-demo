using System.ComponentModel.DataAnnotations;
using TodoDemo.Entities.Enums;

namespace TodoDemo.Contracts;

public record TodoItemResponse(
    Guid Id,
    Guid TodoListId,
    string Title,
    string Notes,
    string Priority,
    bool IsCompleted,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    DateTime? DueAt
);

public record CreateTodoItemRequest
{
    [Required]
    [MaxLength(200)]
    public required string Title { get; init; }

    [MaxLength(500)]
    public string? Notes { get; init; }

    [EnumDataType(typeof(TodoPriority))]
    public string? Priority { get; init; }

    public DateTime? DueAt { get; init; }
}

public record UpdateTodoItemRequest
{
    [MaxLength(200)]
    public string? Title { get; init; }

    [MaxLength(500)]
    public string? Notes { get; init; }

    [EnumDataType(typeof(TodoPriority))]
    public string? Priority { get; init; }

    public bool? IsCompleted { get; init; }

    public DateTime? DueAt { get; init; }
}