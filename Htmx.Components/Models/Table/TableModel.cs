using System.Linq.Expressions;
using Htmx.Components.Models.Builders;

namespace Htmx.Components.Models.Table;

// We have a separate, non-generic class, since razor views don't support generic type params
public interface ITableModel
{
    public string TypeId { get; set; }
    public List<ITableRowContext> Rows { get; set; }
    public List<ITableColumnModel> Columns { get; set; }
    public int PageCount { get; set; }
    public TableState State { get; set; }
    public TableViewPaths TableViewPaths { get; set; }
    public ModelHandler ModelHandler { get; set; }
    public IEnumerable<ActionModel> GetActions();
}


public class TableModel<T, TKey> : ITableModel
    where T : class
{
    public string TypeId { get; set; } = typeof(T).Name;
    public List<TableRowContext<T, TKey>> Rows { get; set; } = new();
    public List<TableColumnModel<T, TKey>> Columns { get; set; } = new();
    public int PageCount { get; set; } = 1;
    public TableState State { get; set; } = new();
    public TableViewPaths TableViewPaths { get; set; } = new();
    public ModelHandler<T, TKey> ModelHandler { get; set; } = default!;
    public Func<IEnumerable<ActionModel>> ActionsFactory { get; set; } = () => Enumerable.Empty<ActionModel>();

    ModelHandler ITableModel.ModelHandler {
        get => ModelHandler;
        set => ModelHandler = (ModelHandler<T, TKey>)value;
    }

    // Explicit implementation of ITableModel
    List<ITableRowContext> ITableModel.Rows
    {
        get => Rows.Cast<ITableRowContext>().ToList();
        set => Rows = value.Cast<TableRowContext<T, TKey>>().ToList();
    }

    List<ITableColumnModel> ITableModel.Columns
    {
        get => Columns.Cast<ITableColumnModel>().ToList();
        set => Columns = value.Cast<TableColumnModel<T, TKey>>().ToList();
    }

    public Expression<Func<T, TKey>> KeySelector { get; internal set; } = default!;

    public IEnumerable<ActionModel> GetActions ()
    {
        return ActionsFactory.Invoke();
    }

}

