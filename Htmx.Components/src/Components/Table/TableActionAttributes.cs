namespace Htmx.Components.Table;

/// <summary>
/// Internal attribute that marks action methods performing table editing operations.
/// Used by the framework's <see cref="Filters.TableOobEditFilter"/> to automatically
/// inject table row updates into out-of-band HTMX responses.
/// </summary>
/// <remarks>
/// This is an internal framework attribute. Actions marked with this attribute should return 
/// a model that implements <see cref="Models.Table.ITableModel"/>. The filter will automatically 
/// generate the appropriate out-of-band updates for table rows and action lists based on the 
/// table model's state.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
internal class TableEditActionAttribute : Attribute { }

/// <summary>
/// Internal attribute that marks action methods refreshing table data.
/// Used by the framework's <see cref="Filters.TableOobRefreshFilter"/> to automatically
/// inject complete table updates into out-of-band HTMX responses.
/// </summary>
/// <remarks>
/// This is an internal framework attribute. Actions marked with this attribute should return 
/// a model that implements <see cref="Models.Table.ITableModel"/>. The filter will automatically 
/// generate out-of-band updates for the table body, header, pagination, and action lists to 
/// reflect the new table state.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
internal class TableRefreshActionAttribute : Attribute { }