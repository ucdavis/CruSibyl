using System.Linq.Expressions;
using System.Text.Json;
using FastExpressionCompiler;
using Htmx.Components.Extensions;

namespace Htmx.Components.Models.Table;

// We have a non-generic interface, since razor views don't support generic type params
public interface ITableColumnModel
{
    string Header { get; set; }
    string DataName { get; set; }
    string Id { get; }
    bool Sortable { get; set; }
    bool Filterable { get; set; }
    bool IsEditable { get; set; }
    ColumnType ColumnType { get; set; }
    Type DataType { get; set; } // Type of the data in the column
    string? CellPartialView { get; set; } // Custom rendering for cell
    string? FilterPartialView { get; set; } // Custom rendering for filter
    public string? CellEditPartialView { get; set; }
    IEnumerable<ActionModel> GetActions(ITableRowContext rowContext);
    object GetValue(ITableRowContext rowContext);  // Extracts value dynamically
    string GetSerializedValue(ITableRowContext rowContext);
    ITableModel Table { get; set; } // Reference to the parent table
}

public enum ColumnType
{
    ValueSelector,
    Hidden,
    Display
}

public class TableCellPartialModel
{
    public required ITableModel Table { get; init; }
    public required ITableRowContext Row { get; init; }
    public required ITableColumnModel Column { get; init; }
}

public class TableColumnModel<T, TKey> : ITableColumnModel where T : class
{
    private Expression<Func<T, object>> _selectorExpression = x => x!;

    // used for referencing a member of T when working with IQueryable<T>
    public Expression<Func<T, object>> SelectorExpression
    {
        get => _selectorExpression;
        set
        {
            _selectorExpression = value;
            // FastExpressionCompiler is a widely used and maintained library that will
            // fallback to the built-in Expression.Compile() if it encounters an error.
            SelectorFunc = value.CompileFast();
        }
    }

    // used for actually pulling values from instance of T when rendering cells
    public Func<T, object> SelectorFunc { get; set; } = x => x!;
    public string Header { get; set; } = "";
    public string DataName { get; set; } = "";
    public string Id => "col_" + DataName.SanitizeForHtmlId();
    public bool Sortable { get; set; } = true;
    public bool Filterable { get; set; } = true;
    public bool IsEditable { get; set; } = false;
    public ColumnType ColumnType { get; set; }
    public Type DataType { get; set; } = typeof(object);
    public string? CellPartialView { get; set; } // Custom rendering for cell
    public string? FilterPartialView { get; set; } // Custom rendering for filter
    public string? CellEditPartialView { get; set; }
    public ITableModel Table { get; set; } = default!;

    /// <summary>
    /// A delegate that extends filtering of a <see cref="IQueryable<typeparamref name="T"/>"/>   using a single value comparison
    /// </summary>
    public Func<IQueryable<T>, string, IQueryable<T>>? Filter { get; set; }

    /// <summary>
    /// A delegate that extends filtering of a <see cref="IQueryable<typeparamref name="T"/>"/>  using a two value range comparison
    /// </summary>
    public Func<IQueryable<T>, string, string, IQueryable<T>>? RangeFilter { get; set; }

    /// <summary>
    /// A delegate that generates one or more <see cref="ActionModel"/>s that can be mapped to buttons in a view
    /// </summary>
    public Func<TableRowContext<T, TKey>, IEnumerable<ActionModel>> ActionsFactory { get; set; } = _ => [];

    public IEnumerable<ActionModel> GetActions(ITableRowContext rowContext)
    {
        if (rowContext.Item is T typedItem)
        {
            return ActionsFactory((TableRowContext<T, TKey>)rowContext);
        }
        return [];
    }

    // Implement the interface method for non-generic use
    public object GetValue(ITableRowContext rowContext)
    {
        if (rowContext.Item is T typedItem)
        {
            return SelectorFunc(typedItem);
        }
        return "";
    }

    public string GetSerializedValue(ITableRowContext rowContext)
    {
        if (rowContext.Item is T typedItem)
        {
            return JsonSerializer.Serialize(SelectorFunc(typedItem));
        }
        return "";
    }
}
