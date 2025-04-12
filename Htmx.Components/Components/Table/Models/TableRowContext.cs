using System.Text.Json;
using Htmx.Components.Extensions;

namespace Htmx.Components.Table.Models;

public interface ITableRowContext
{
    object Item { get; }
    string RowId { get; } // E.g., "row_5f3e"
    int PageIndex { get; } // Row's index within current page
    string Key { get; }
    bool IsEditing { get; }
}

public class TableRowContext<T, TKey> : ITableRowContext
{
    public required T Item { get; init; }
    public string RowId => "row_" + _stringKey.SanitizeForHtmlId();
    public int PageIndex { get; set; } = 0; // Row's index within current page
    private TKey _key = default!;
    public required TKey Key
    {
        get => _key;
        init
        {
            _key = value;
            _stringKey = JsonSerializer.Serialize(value);
        }
    }
    public bool IsEditing { get; set; }

    private string _stringKey = "";
    string ITableRowContext.Key => _stringKey;
    object ITableRowContext.Item => Item!;


}