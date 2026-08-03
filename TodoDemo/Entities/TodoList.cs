using System.ComponentModel.DataAnnotations;

namespace TodoDemo.Entities;

/// <summary>
/// A collection of todo items
/// </summary>
public class TodoList
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TodoItem> Items { get; set; } = [];

}