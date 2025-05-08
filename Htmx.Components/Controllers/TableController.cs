using System.Text.Json;
using Htmx.Components.Models;
using Htmx.Components.Services;
using Htmx.Components.Table;
using Htmx.Components.Table.Models;
using Htmx.Components.ViewResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Htmx.Components.Controllers;

[Route("Table")]
public class TableController : Controller
{
    private readonly ITableProvider _tableProvider;
    private readonly IModelRegistry _modelRegistry;

    public TableController(ITableProvider tableProvider, IModelRegistry modelRegistry)
    {
        _tableProvider = tableProvider;
        _modelRegistry = modelRegistry;
    }


    [HttpPost("{typeId}/SaveRow")]
    public async Task<IActionResult> SaveRow(string typeId)
    {
        var modelHandler = _modelRegistry.GetModelHandler(typeId);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var method = typeof(TableController).GetMethod(nameof(_SaveRow),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.MakeGenericMethod(modelHandler.ModelType, modelHandler.KeyType)!;

        var result = method.Invoke(this, new object[] { modelHandler }) as Task<IActionResult>;
        return await result!;
    }

    private async Task<IActionResult> _SaveRow<T, TKey>(ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        if (modelHandler.InsertModel == null)
            return BadRequest($"SaveModel not defined for type '{modelHandler.TypeId}'.");

        var pageState = this.GetPageState();
        var editingItem = pageState.Get<T>("Table", "EditingItem")!;
        var editingExistingRecord = pageState.Get<bool>("Table", "EditingExistingRecord")!;
        var tableModel = modelHandler.BuildTableModel!();
        if (editingExistingRecord)
        {
            await modelHandler.UpdateModel!(editingItem);
            tableModel.Rows.Add(new TableRowContext<T, TKey>
            {
                Item = editingItem,
                Key = modelHandler.KeySelectorFunc(editingItem),
                TargetDisposition = OobTargetDisposition.OuterHtml,
            });
        }
        else
        {
            await modelHandler.InsertModel!(editingItem);
            tableModel.Rows.Add(new TableRowContext<T, TKey>
            {
                Item = null!,
                StringKey = "new",
                TargetDisposition = OobTargetDisposition.Delete,
            });
            tableModel.Rows.Add(new TableRowContext<T, TKey>
            {
                Item = editingItem,
                Key = modelHandler.KeySelectorFunc(editingItem),
                TargetDisposition = OobTargetDisposition.AfterBegin,
                TargetSelector = "#table-body",
            });
        }


        pageState.ClearKey("Table", "EditingItem");
        pageState.ClearKey("Table", "EditingExistingRecord");

        return _tableProvider.RefreshEditViews(tableModel);
    }

    [HttpPost("{typeId}/CancelEditRow")]
    public async Task<IActionResult> CancelEditRow(string typeId)
    {
        var modelHandler = _modelRegistry.GetModelHandler(typeId);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var method = typeof(TableController).GetMethod(nameof(_CancelEditRow),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.MakeGenericMethod(modelHandler.ModelType, modelHandler.KeyType)!;

        var result = method.Invoke(this, new object[] { modelHandler }) as Task<IActionResult>;
        return await result!;
    }

    private async Task<IActionResult> _CancelEditRow<T, TKey>(ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        var tableModel = modelHandler.BuildTableModel!();
        var pageState = this.GetPageState();
        if (pageState.Get<bool>("Table", "EditingExistingRecord"))
        {
            var editingItem = pageState.Get<T>("Table", "EditingItem")!;
            var editingKey = modelHandler.KeySelectorFunc(editingItem);

            var originalItem = await modelHandler.GetQueryable!()
                .Where(modelHandler.GetKeyPredicate(editingKey))
                .SingleAsync();

            tableModel.Rows.Add(new TableRowContext<T, TKey>
            {
                Item = originalItem,
                Key = editingKey,
                TargetDisposition = OobTargetDisposition.OuterHtml,
            });
        }
        else
        {
            tableModel.Rows.Add(new TableRowContext<T, TKey>
            {
                Item = null!,
                StringKey = "new",
                TargetDisposition = OobTargetDisposition.Delete,
            });
        }

        pageState.ClearKey("Table", "EditingItem");
        pageState.ClearKey("Table", "EditingExistingRecord");
        return _tableProvider.RefreshEditViews(tableModel);
    }

    [HttpPost("{typeId}/DeleteRow")]
    public async Task<IActionResult> DeleteRow(string typeId, string key)
    {
        var modelHandler = _modelRegistry.GetModelHandler(typeId);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var method = typeof(TableController).GetMethod(nameof(_DeleteRow),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.MakeGenericMethod(modelHandler.ModelType, modelHandler.KeyType)!;

        var result = method.Invoke(this, new object[] { key, modelHandler }) as Task<IActionResult>;
        return await result!;
    }

    private async Task<IActionResult> _DeleteRow<T, TKey>(string stringKey, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        if (modelHandler.DeleteModel == null)
            return BadRequest($"DeleteModel not defined for type '{modelHandler.TypeId}'.");
        
        var key = (TKey)JsonSerializer.Deserialize(stringKey, modelHandler.KeyType)!;

        await modelHandler.DeleteModel!(key);

        var pageState = this.GetPageState();
        var tableModel = modelHandler.BuildTableModel!();
        tableModel.Rows.Add(new TableRowContext<T, TKey>
        {
            Item = default!,
            Key = key,
            TargetDisposition = OobTargetDisposition.Delete,
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }

    [HttpPost("{typeId}/EditRow")]
    public async Task<IActionResult> EditRow(string typeId, string key)
    {
        var modelHandler = _modelRegistry.GetModelHandler(typeId);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var method = typeof(TableController).GetMethod(nameof(_EditRow),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.MakeGenericMethod(modelHandler.ModelType, modelHandler.KeyType)!;

        var result = method.Invoke(this, new object[] { key, modelHandler }) as Task<IActionResult>;
        return await result!;
    }

    private async Task<IActionResult> _EditRow<T, TKey>(string stringKey, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        var key = (TKey)JsonSerializer.Deserialize(stringKey, modelHandler.KeyType)!;
        var editingItem = await modelHandler.GetQueryable!()
            .Where(modelHandler.GetKeyPredicate(key))
            .SingleOrDefaultAsync();
        if (editingItem == null)
            return BadRequest($"Model with key '{stringKey}' not found.");
        var pageState = this.GetPageState();
        pageState.Set("Table", "EditingItem", editingItem);
        pageState.Set("Table", "EditingExistingRecord", true);

        var tableModel = modelHandler.BuildTableModel!();
        tableModel.Rows.Add(new TableRowContext<T, TKey>
        {
            Item = editingItem,
            Key = key,
            TargetDisposition = OobTargetDisposition.OuterHtml,
            IsEditing = true,
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }

    [HttpPost("{typeId}/SetPage")]
    public async Task<IActionResult> SetPage(string typeId, int page)
    {
        var modelHandler = _modelRegistry.GetModelHandler(typeId);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var method = typeof(TableController).GetMethod(nameof(_SetPage),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.MakeGenericMethod(modelHandler.ModelType, modelHandler.KeyType)!;

        var result = method.Invoke(this, new object[] { page, modelHandler }) as Task<IActionResult>;
        return await result!;
    }

    private async Task<IActionResult> _SetPage<T, TKey>(int page, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        var pageState = this.GetPageState();
        var tableState = pageState.GetOrCreate<TableState>("Table", "State", () => new());
        tableState.Page = page;
        pageState.Set("Table", "State", tableState);
        var tableModel = modelHandler.BuildTableModel!();
        await _tableProvider.FetchPage(tableModel, modelHandler.GetQueryable!(), tableState);

        return _tableProvider.RefreshAllViews(tableModel);
    }

    [HttpPost("{typeId}/SetPageSize")]
    public async Task<IActionResult> SetPageSize(string typeId, int pageSize)
    {
        var modelHandler = _modelRegistry.GetModelHandler(typeId);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var method = typeof(TableController).GetMethod(nameof(_SetPageSize),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.MakeGenericMethod(modelHandler.ModelType, modelHandler.KeyType)!;

        var result = method.Invoke(this, new object[] { pageSize, modelHandler }) as Task<IActionResult>;
        return await result!;
    }

    private async Task<IActionResult> _SetPageSize<T, TKey>(int pageSize, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        var pageState = this.GetPageState();
        var tableState = pageState.GetOrCreate<TableState>("Table", "State", () => new());
        tableState.PageSize = pageSize;
        pageState.Set("Table", "State", tableState);
        var tableModel = modelHandler.BuildTableModel!();
        await _tableProvider.FetchPage(tableModel, modelHandler.GetQueryable!(), tableState);

        return _tableProvider.RefreshAllViews(tableModel);
    }

    [HttpPost("{typeId}/SetSort")]
    public async Task<IActionResult> SetSort(string typeId, string column, string direction)
    {
        var modelHandler = _modelRegistry.GetModelHandler(typeId);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var method = typeof(TableController).GetMethod(nameof(_SetSort),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.MakeGenericMethod(modelHandler.ModelType, modelHandler.KeyType)!;

        var result = method.Invoke(this, new object[] { column, direction, modelHandler }) as Task<IActionResult>;
        return await result!;
    }

    private async Task<IActionResult> _SetSort<T, TKey>(string column, string direction, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        var pageState = this.GetPageState();
        var tableState = pageState.GetOrCreate<TableState>("Table", "State", () => new());
        tableState.SortColumn = column;
        tableState.SortDirection = direction;
        pageState.Set("Table", "State", tableState);
        var tableModel = modelHandler.BuildTableModel!();
        await _tableProvider.FetchPage(tableModel, modelHandler.GetQueryable!(), tableState);

        return _tableProvider.RefreshAllViews(tableModel);
    }

    [HttpPost("{typeId}/SetCell")]
    public IActionResult SetCell(string typeId, string propertyName, string value)
    {
        var modelHandler = _modelRegistry.GetModelHandler(typeId);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var method = typeof(TableController).GetMethod(nameof(_SetCell),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.MakeGenericMethod(modelHandler.ModelType, modelHandler.KeyType)!;

        var result = method.Invoke(this, new object[] { propertyName, value, modelHandler }) as IActionResult;
        return result!;
    }

    private IActionResult _SetCell<T, TKey>(string propertyName, string value, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        var pageState = this.GetPageState();
        var tableState = pageState.GetOrCreate<TableState>("Table", "State", () => new());
        var editingItem = pageState.Get<T>("Table", "EditingItem")!;
        var property = typeof(T).GetProperty(propertyName);
        if (property == null)
            return BadRequest($"Property '{propertyName}' not found.");

        try
        {
            var convertedValue = Convert.ChangeType(value, property.PropertyType);
            property.SetValue(editingItem, convertedValue);
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to set property '{propertyName}': {ex.Message}");
        }

        pageState.Set("Table", "EditingItem", editingItem);
        // We return a MultiSwapViewResult to allow the PageState to piggyback on the response
        return new MultiSwapViewResult();
    }

    [HttpPost("{typeId}/SetFilter")]
    public async Task<IActionResult> SetFilter(string typeId, string column, string filter, int input)
    {
        var modelHandler = _modelRegistry.GetModelHandler(typeId);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var method = typeof(TableController).GetMethod(nameof(_SetFilter),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.MakeGenericMethod(modelHandler.ModelType, modelHandler.KeyType)!;

        var result = method.Invoke(this, new object[] { column, filter, input, modelHandler }) as Task<IActionResult>;
        return await result!;
    }

    private async Task<IActionResult> _SetFilter<T, TKey>(string column, string filter, int input, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        var tableModel = modelHandler.BuildTableModel!();
        var columnModel = tableModel.Columns.FirstOrDefault(c => c.DataName == column);
        if (columnModel == null)
            return BadRequest($"Column '{column}' not found.");

        if (!columnModel.Filterable || (columnModel.RangeFilter == null && columnModel.Filter == null))
            return BadRequest($"Column '{column}' is not filterable.");

        var pageState = this.GetPageState();
        var tableState = pageState.GetOrCreate<TableState>("Table", "State", () => new());
        if (columnModel.Filter != null)
        {
            if (string.IsNullOrEmpty(filter))
                tableState.Filters.Remove(column);
            else
                tableState.Filters[column] = filter;
        }
        else if (columnModel.RangeFilter != null)
        {
            (var from, var to) = tableState.RangeFilters.TryGetValue(column, out var range) ? range : ("", "");
            if (input == 1)
                from = filter;
            else if (input == 2)
                to = filter;
            else
                return BadRequest($"Invalid input value: {input}");
            tableState.RangeFilters[column] = (from, to);
        }

        pageState.Set("Table", "State", tableState);
        await _tableProvider.FetchPage(tableModel, modelHandler.GetQueryable!(), tableState);
        return _tableProvider.RefreshAllViews(tableModel);
    }

    [HttpPost("{typeId}/NewTableRow")]
    public IActionResult NewTableRow(string typeId)
    {
        var modelHandler = _modelRegistry.GetModelHandler(typeId);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var method = typeof(TableController).GetMethod(nameof(_NewTableRow),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.MakeGenericMethod(modelHandler.ModelType, modelHandler.KeyType)!;

        var result = method.Invoke(this, new object[] { modelHandler }) as IActionResult;
        return result!;
    }

    private IActionResult _NewTableRow<T, TKey>(ModelHandler<T, TKey> modelHandler)
        where T : class, new()
    {
        var editingItem = new T();

        var pageState = this.GetPageState();
        pageState.Set("Table", "EditingItem", editingItem);
        pageState.Set("Table", "EditingExistingRecord", false);

        var tableModel = modelHandler.BuildTableModel!();
        tableModel.Rows.Add(new TableRowContext<T, TKey>
        {
            Item = editingItem,
            TargetDisposition = OobTargetDisposition.AfterBegin,
            TargetSelector = "#table-body",
            StringKey = "new",
            IsEditing = true,
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }
}