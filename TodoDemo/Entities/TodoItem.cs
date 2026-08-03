using System.ComponentModel.DataAnnotations;
using TodoDemo.Entities.Enums;

namespace TodoDemo.Entities;

/// <summary>
/// A single todo item consisting of a title, notes, priority, and completion status
/// </summary>
public class TodoItem
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    public TodoPriority Priority { get; set; } = TodoPriority.Medium;

    public bool IsCompleted { get; set; } = false;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; } = null;

    public DateTime? DueAt { get; set; } = null;

    // FK + navigation properties
    public Guid TodoListId { get; init; }
    public TodoList? TodoList { get; init; }
}