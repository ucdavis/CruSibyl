using System.Linq.Expressions;

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
    public Task<IEnumerable<ActionModel>> GetActions();
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
    public List<Func<TableModel<T, TKey>, Task<IEnumerable<ActionModel>>>> ActionsFactories { get; set; } = [];
    public Expression<Func<T, TKey>> KeySelector { get; internal set; } = default!;

    public TableModel(TableModelConfig<T, TKey> config)
    {
        TypeId = config.TypeId ?? typeof(T).Name;
        Columns = config.Columns;
        TableViewPaths = config.TableViewPaths ?? new TableViewPaths();
        ModelHandler = config.ModelHandler ?? throw new ArgumentNullException(nameof(config.ModelHandler));
        ActionsFactories = config.ActionsFactories;
        KeySelector = config.KeySelector ?? throw new ArgumentNullException(nameof(config.KeySelector));
    }

    ModelHandler ITableModel.ModelHandler
    {
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

    public async Task<IEnumerable<ActionModel>> GetActions()
    {
        var results = new List<ActionModel>();
        foreach (var factory in ActionsFactories)
        {
            var actions = await factory(this);
            if (actions != null)
                results.AddRange(actions);
        }
        return results;
    }
}

public class TableModelConfig<T, TKey>
    where T : class
{
    public string? TypeId { get; set; }
    public Expression<Func<T, TKey>>? KeySelector { get; set; }
    public ModelHandler<T, TKey>? ModelHandler { get; set; }
    public TableViewPaths TableViewPaths { get; set; } = new();
    public List<TableColumnModel<T, TKey>> Columns { get; } = new();
    public List<Func<TableModel<T, TKey>, Task<IEnumerable<ActionModel>>>> ActionsFactories { get; set; } = [];
}