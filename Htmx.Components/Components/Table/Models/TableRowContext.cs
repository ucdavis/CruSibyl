using System.Text.Json;
using Htmx.Components.Extensions;

namespace Htmx.Components.Table.Models;

public interface ITableRowContext
{
    object Item { get; }
    string RowId { get; } // E.g., "row_5f3e"
    int PageIndex { get; } // Row's index within current page
    string Key { get; }
    RowAction RowAction { get; }
}

public enum RowAction
{
    Display,
    Edit,
    Delete,
    Insert
}

public class TableRowContext<T, TKey> : ITableRowContext
{
    public required T Item { get; set; }
    public string RowId => "row_" + StringKey.SanitizeForHtmlId();
    public int PageIndex { get; set; } = 0; // Row's index within current page
    private TKey _key = default!;
    public TKey Key
    {
        get => _key;
        set
        {
            _key = value;
            StringKey = JsonSerializer.Serialize(value);
        }
    }
    public RowAction RowAction { get; set; }
    public string StringKey { get; set; } = "";
    string ITableRowContext.Key => StringKey;
    object ITableRowContext.Item => Item!;
}
