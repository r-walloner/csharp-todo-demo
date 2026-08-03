namespace TodoDemo.Contracts;

/// <summary>
/// A generic paginated response for returning a list of items efficiently.
/// </summary>
/// <typeparam name="T">The type of items in the paginated response.</typeparam>
/// <param name="Items">The list of items on the current page.</param>
/// <param name="Page">The current page number.</param>
/// <param name="PageSize">The number of items per page.</param>
/// <param name="TotalCount">The total number of items across all pages.</param>
public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}