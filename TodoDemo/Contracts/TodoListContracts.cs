using System.ComponentModel.DataAnnotations;

namespace TodoDemo.Contracts;

public record TodoListResponse(
    Guid Id,
    string Title,
    string Description,
    int ItemCount,
    int OpenItemCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateTodoListRequest
{
    [Required]
    [MaxLength(200)]
    public required string Title { get; init; }

    [MaxLength(500)]
    public string? Description { get; init; }
}

public record UpdateTodoListRequest
{
    [MaxLength(200)]
    public string? Title { get; init; }

    [MaxLength(500)]
    public string? Description { get; init; }
}