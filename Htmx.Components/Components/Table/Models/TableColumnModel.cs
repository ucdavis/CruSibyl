using System.Linq.Expressions;
using FastExpressionCompiler;
using Htmx.Components.Action;

namespace Htmx.Components.Table.Models;

// We have a non-generic interface, since razor views don't support generic type params
public interface ITableColumnModel
{
    string Header { get; set; }
    bool Sortable { get; set; }
    bool Filterable { get; set; }
    public bool IsHidden { get; set; }
    string? CellPartialView { get; set; } // Custom rendering for cell
    string? FilterPartialView { get; set; } // Custom rendering for filter
    public string? CellEditPartialView { get; set; }
    IEnumerable<ActionModel> GetActions(ITableRowContext rowContext);
    object GetValue(ITableRowContext rowContext);  // Extracts value dynamically
}

public class TableCellPartialModel
{
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
    public bool Sortable { get; set; } = true;
    public bool Filterable { get; set; } = true;
    public bool IsHidden { get; set; } = false;
    public string? CellPartialView { get; set; } // Custom rendering for cell
    public string? FilterPartialView { get; set; } // Custom rendering for filter
    public string? CellEditPartialView { get; set; }

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
        return string.Empty;
    }    
}
