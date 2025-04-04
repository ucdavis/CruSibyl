using System.Linq.Expressions;
using FastExpressionCompiler;

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
    object GetValue(object item);  // Extracts value dynamically
}


public class TableColumnModel<T> : ITableColumnModel where T : class
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

    /// <summary>
    /// A delegate that extends filtering of a <see cref="IQueryable<typeparamref name="T"/>"/>   using a single value comparison
    /// </summary>
    public Func<IQueryable<T>, string, IQueryable<T>>? Filter { get; set; }

    /// <summary>
    /// A delegate that extends filtering of a <see cref="IQueryable<typeparamref name="T"/>"/>  using a two value range comparison
    /// </summary>
    public Func<IQueryable<T>, string, string, IQueryable<T>>? RangeFilter { get; set; }

    // Implement the interface method for non-generic use
    public object GetValue(object item)
    {
        if (item is T typedItem)
        {
            return SelectorFunc(typedItem);
        }
        return string.Empty;
    }    
}
