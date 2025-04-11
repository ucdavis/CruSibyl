namespace Htmx.Components.Table.Models;

public interface ITableRowContext
{
    object Item { get; }
    string RowId { get; } // E.g., "row_5f3e"
    int PageIndex { get; } // Row's index within current page
    Dictionary<string, object> Keys { get; } // ID fields
}

public class TableRowContext<T> : ITableRowContext
{
    public required T Item { get; init; }
    public required string RowId { get; init; } // E.g., "row_5f3e"
    public required int PageIndex { get; init; } // Row's index within current page
    public Dictionary<string, object> Keys { get; init; } = new(); // ID fields
    object ITableRowContext.Item => Item!;
}