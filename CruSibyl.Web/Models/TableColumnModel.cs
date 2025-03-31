using System.Linq.Expressions;
using FastExpressionCompiler;

namespace CruSibyl.Web.Models;

// We have a non-generic interface, since razor views don't support generic type params
public interface ITableColumnModel
{
    string Header { get; set; }
    bool Sortable { get; set; }
    bool Filterable { get; set; }
    string? FilterType { get; set; }
    string? FilterValue { get; set; }
    string? CellPartialView { get; set; } // Custom rendering for cell
    string? FilterPartialView { get; set; } // Custom rendering for filter
    object GetValue(object item);  // Extracts value dynamically
}


public class TableColumnModel<T> : ITableColumnModel where T : class
{
    private Expression<Func<T, object>> _valueExpression = x => x!;
    public Expression<Func<T, object>> ValueExpression
    {
        get => _valueExpression;
        set
        {
            _valueExpression = value;
            // FastExpressionCompiler is a widely used and maintained library that will
            // fallback to the built-in Expression.Compile() if it encounters an error.
            ValueFunc = value.CompileFast();
        }
    }

    public Func<T, object> ValueFunc { get; set; } = x => x!;
    public string Header { get; set; } = "";
    public bool Sortable { get; set; } = false;
    public bool Filterable { get; set; } = false;
    public string? FilterType { get; set; } = "contains"; 
    public string? FilterValue { get; set; }
    public string? CellPartialView { get; set; } // Custom rendering for cell
    public string? FilterPartialView { get; set; } // Custom rendering for filter

    // Dynamic filter expression
    public Func<IQueryable<T>, string, IQueryable<T>>? FilterExpression { get; set; }

    // Range filter expression
    public Func<IQueryable<T>, string, string, IQueryable<T>>? RangeFilterExpression { get; set; }

    // Implement the interface method for non-generic use
    public object GetValue(object item)
    {
        if (item is T typedItem)
        {
            return ValueFunc(typedItem);
        }
        return string.Empty;
    }    
}
