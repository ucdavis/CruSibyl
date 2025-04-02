using CruSibyl.Web.Results;
using CruSibyl.Web.Table.Models;

namespace CruSibyl.Web.Table;

/// <summary>
/// Just a simple wrapper of <see cref="MultiSwapViewResult"/> so that callers don't
/// need to remember what partials to include.
/// </summary>
public class RefreshTableViewResult : MultiSwapViewResult
{
    public RefreshTableViewResult(TableModel tableModel) : base(
            ("_TableBody", tableModel),
            ("_TablePagination", tableModel),
            ("_TableHeader", tableModel)
        )
    { }
}