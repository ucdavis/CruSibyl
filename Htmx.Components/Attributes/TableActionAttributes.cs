namespace Htmx.Components.Attributes;

/// <summary>
/// Marks action methods that perform table editing operations such as create, update, or delete.
/// This attribute is used by <see cref="Filters.TableOobEditFilter"/> to automatically
/// inject table row updates into out-of-band HTMX responses.
/// </summary>
/// <remarks>
/// Actions marked with this attribute should return a model that implements <see cref="Models.Table.ITableModel"/>.
/// The filter will automatically generate the appropriate out-of-band updates for table rows
/// and action lists based on the table model's state.
/// </remarks>
/// <example>
/// <code>
/// [HttpPost]
/// [TableEditAction]
/// public async Task&lt;IActionResult&gt; Create(CreateUserModel model)
/// {
///     // Create logic here
///     return Ok(tableModel);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public class TableEditActionAttribute : Attribute { }

/// <summary>
/// Marks action methods that refresh table data such as pagination, sorting, or filtering.
/// This attribute is used by <see cref="Filters.TableOobRefreshFilter"/> to automatically
/// inject complete table updates into out-of-band HTMX responses.
/// </summary>
/// <remarks>
/// Actions marked with this attribute should return a model that implements <see cref="Models.Table.ITableModel"/>.
/// The filter will automatically generate out-of-band updates for the table body, header,
/// pagination, and action lists to reflect the new table state.
/// </remarks>
/// <example>
/// <code>
/// [HttpPost]
/// [TableRefreshAction]
/// public async Task&lt;IActionResult&gt; SetPage(int page)
/// {
///     // Pagination logic here
///     return Ok(tableModel);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public class TableRefreshActionAttribute : Attribute { }