namespace Htmx.Components.Table.Models;

// We have a separate, non-generic class, since razor views don't support generic type params
public class TableViewModel
{
    public List<ITableRowContext> Data { get; set; } = new();    
    public List<ITableColumnModel> Columns { get; set; } = new();
    public int PageCount { get; set; }
    public TableQueryParams Query { get; set; } = new TableQueryParams();
    public TableViewPaths TableViewPaths { get; set; } = new();
}

public class TableModel<T, TKey> where T : class
{
    public List<TableRowContext<T, TKey>> Data { get; set; } = new();
    public List<TableColumnModel<T, TKey>> Columns { get; set; } = new();
    public int PageCount { get; set; } = 1;
    public TableQueryParams Query { get; set; } = new TableQueryParams();    
    public TableViewPaths TableViewPaths { get; set; } = new();

    // Converts to a non-generic TableModel for the view
    public TableViewModel ToViewModel()
    {
        return new TableViewModel
        {
            Data = Data.Cast<ITableRowContext>().ToList(),
            Columns = Columns.Cast<ITableColumnModel>().ToList(),
            PageCount = PageCount,
            Query = Query,
            TableViewPaths = TableViewPaths
        };
    }
}

