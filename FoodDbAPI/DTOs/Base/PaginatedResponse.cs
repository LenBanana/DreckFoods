namespace FoodDbAPI.DTOs.Base;

public class PaginatedResponse
{
    /// <summary>
    /// The page number to retrieve.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// The number of items per page.
    /// </summary>
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// The total number of pages available based on the total item count and page size.
    /// </summary>
    public int TotalPages { get; set; }
}