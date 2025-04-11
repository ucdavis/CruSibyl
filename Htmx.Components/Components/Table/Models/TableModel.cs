namespace Htmx.Components.Table.Models;

// We have a separate, non-generic class, since razor views don't support generic type params
public class TableModel
{
    public List<ITableRowContext> Data { get; set; } = new();    
    public List<ITableColumnModel> Columns { get; set; } = new();
    public int PageCount { get; set; }
    public TableQueryParams Query { get; set; } = new TableQueryParams();    
}

public class TableModel<T> where T : class
{
    public List<TableRowContext<T>> Data { get; set; } = new();
    public List<TableColumnModel<T>> Columns { get; set; } = new();
    public int PageCount { get; set; } = 1;
    public TableQueryParams Query { get; set; } = new TableQueryParams();    

    // Converts to a non-generic TableModel for the view
    public TableModel ToNonGeneric()
    {
        return new TableModel
        {
            Data = Data.Cast<ITableRowContext>().ToList(),
            Columns = Columns.Cast<ITableColumnModel>().ToList(),
            PageCount = PageCount,
            Query = Query
        };
    }
}

