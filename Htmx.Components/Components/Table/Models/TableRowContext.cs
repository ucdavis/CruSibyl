using System.Text.Json;
using Htmx.Components.Extensions;

namespace Htmx.Components.Table.Models;

public interface ITableRowContext
{
    object Item { get; }
    string RowId { get; } // E.g., "row_5f3e"
    int PageIndex { get; } // Row's index within current page
    string Key { get; }
    RowType RowType { get; }
}

public enum RowType
{
    ReadOnly,
    Editable,
    Hidden,
}

/// <summary>
/// Represents a hidden row for oob swaps that remove a row from the table 
/// </summary>
public class HiddenRowContext : ITableRowContext
{
    public string RowId => "row_" + Key.SanitizeForHtmlId();
    public int PageIndex { get; set; } = 0; // Row's index within current page
    public required string Key { get; init; }
    public object Item => null!;
    public RowType RowType => RowType.Hidden;
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
    public RowType RowType { get; set; }
    public string StringKey { get; set; } = "";
    string ITableRowContext.Key => StringKey;
    object ITableRowContext.Item => Item!;
}
