using Htmx.Components.Action;

namespace Htmx.Components.Table.Models;

// We have a separate, non-generic class, since razor views don't support generic type params
public interface ITableModel
{
    public List<ITableRowContext> Rows { get; set; }
    public List<ITableColumnModel> Columns { get; set; }
    public int PageCount { get; set; }
    public TableQueryParams Query { get; set; }
    public TableViewPaths TableViewPaths { get; set; }
    public IEnumerable<ActionModel> GetActions();
}

public class TableModel<T, TKey> : ITableModel
    where T : class
{
    public List<TableRowContext<T, TKey>> Rows { get; set; } = new();
    public List<TableColumnModel<T, TKey>> Columns { get; set; } = new();
    public int PageCount { get; set; } = 1;
    public TableQueryParams Query { get; set; } = new();
    public TableViewPaths TableViewPaths { get; set; } = new();
    public Func<TableModel<T, TKey>, IEnumerable<ActionModel>> ActionsFactory = _ => [];

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

    public IEnumerable<ActionModel> GetActions() => ActionsFactory(this);

}

