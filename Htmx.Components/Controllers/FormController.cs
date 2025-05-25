using System.Text.Json;
using Htmx.Components.Authorization;
using Htmx.Components.Models;
using Htmx.Components.Models.Table;
using Htmx.Components.Services;
using Htmx.Components.Table;
using Htmx.Components.Utilities;
using Htmx.Components.ViewResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Operations = Htmx.Components.Constants.Authorization.Operations;

namespace Htmx.Components.Controllers;

[Route("Form")]
public class FormController : Controller
{
    private readonly ITableProvider _tableProvider;
    private readonly IModelRegistry _modelRegistry;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPermissionRequirementFactory _permissionRequirementFactory;

    public FormController(ITableProvider tableProvider, IModelRegistry modelRegistry,
        IAuthorizationService authorizationService, IPermissionRequirementFactory permissionRequirementFactory)
    {
        _tableProvider = tableProvider;
        _modelRegistry = modelRegistry;
        _authorizationService = authorizationService;
        _permissionRequirementFactory = permissionRequirementFactory;
    }


    [HttpPost("{typeId}/{modelUI}/Save")]
    public async Task<IActionResult> Save(string typeId, ModelUI modelUI)
    {
        var modelHandler = await _modelRegistry.GetModelHandler(typeId, modelUI);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var result = await GenericMethodInvoker.InvokeAsync<IActionResult>(
            this,
            nameof(_Save),
            [modelHandler.ModelType, modelHandler.KeyType],
            modelHandler);

        return result;
    }

    private async Task<IActionResult> _Save<T, TKey>(ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        if (modelHandler.CreateModel == null)
            return BadRequest($"SaveModel not defined for type '{modelHandler.TypeId}'.");

        var pageState = this.GetPageState();
        var editingItem = pageState.Get<T>("Table", "EditingItem")!;
        var editingExistingRecord = pageState.Get<bool>("Table", "EditingExistingRecord")!;
        var tableModel = await modelHandler.BuildTableModel();
        if (editingExistingRecord)
        {
            if (!await IsAuthorized(modelHandler.TypeId, Operations.Update))
                return Forbid();
            await modelHandler.UpdateModel!(editingItem);
            tableModel.Rows.Add(new TableRowContext<T, TKey>
            {
                Item = editingItem,
                ModelHandler = modelHandler,
                Key = modelHandler.KeySelectorFunc(editingItem),
                TargetDisposition = OobTargetDisposition.OuterHtml,
            });
        }
        else
        {
            if (!await IsAuthorized(modelHandler.TypeId, Operations.Create))
                return Forbid();
            await modelHandler.CreateModel!(editingItem);
            tableModel.Rows.Add(new TableRowContext<T, TKey>
            {
                Item = null!,
                ModelHandler = modelHandler,
                StringKey = "new",
                TargetDisposition = OobTargetDisposition.Delete,
            });
            tableModel.Rows.Add(new TableRowContext<T, TKey>
            {
                Item = editingItem,
                ModelHandler = modelHandler,
                Key = modelHandler.KeySelectorFunc(editingItem),
                TargetDisposition = OobTargetDisposition.AfterBegin,
                TargetSelector = "#table-body",
            });
        }


        pageState.ClearKey("Table", "EditingItem");
        pageState.ClearKey("Table", "EditingExistingRecord");

        return _tableProvider.RefreshEditViews(tableModel);
    }

    [HttpPost("{typeId}/{modelUI}/CancelEdit")]
    public async Task<IActionResult> CancelEdit(string typeId, ModelUI modelUI)
    {
        var modelHandler = await _modelRegistry.GetModelHandler(typeId, modelUI);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var result = await GenericMethodInvoker.InvokeAsync<IActionResult>(
            this,
            nameof(_CancelEdit),
            [modelHandler.ModelType, modelHandler.KeyType],
            modelHandler);

        return result!;
    }

    private async Task<IActionResult> _CancelEdit<T, TKey>(ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        var tableModel = await modelHandler.BuildTableModel();
        var pageState = this.GetPageState();
        if (pageState.Get<bool>("Table", "EditingExistingRecord"))
        {
            // Check if the user is authorized to read the item
            if (!await IsAuthorized(modelHandler.TypeId, Operations.Read))
                return Forbid();

            var editingItem = pageState.Get<T>("Table", "EditingItem")!;
            var editingKey = modelHandler.KeySelectorFunc(editingItem);

            var originalItem = await modelHandler.GetQueryable!()
                .Where(modelHandler.GetKeyPredicate(editingKey))
                .SingleAsync();

            tableModel.Rows.Add(new TableRowContext<T, TKey>
            {
                Item = originalItem,
                ModelHandler = modelHandler,
                Key = editingKey,
                TargetDisposition = OobTargetDisposition.OuterHtml,
            });
        }
        else
        {
            tableModel.Rows.Add(new TableRowContext<T, TKey>
            {
                Item = null!,
                ModelHandler = modelHandler,
                StringKey = "new",
                TargetDisposition = OobTargetDisposition.Delete,
            });
        }

        pageState.ClearKey("Table", "EditingItem");
        pageState.ClearKey("Table", "EditingExistingRecord");
        return _tableProvider.RefreshEditViews(tableModel);
    }

    [HttpPost("{typeId}/{modelUI}/Delete")]
    public async Task<IActionResult> Delete(string typeId, ModelUI modelUI, string key)
    {
        var modelHandler = await _modelRegistry.GetModelHandler(typeId, modelUI);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var result = await GenericMethodInvoker.InvokeAsync<IActionResult>(
            this,
            nameof(_Delete),
            [modelHandler.ModelType, modelHandler.KeyType],
            key, modelHandler);
        return result!;
    }

    private async Task<IActionResult> _Delete<T, TKey>(string stringKey, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        if (modelHandler.DeleteModel == null)
            return BadRequest($"DeleteModel not defined for type '{modelHandler.TypeId}'.");

        if (!await IsAuthorized(modelHandler.TypeId, Operations.Delete))
            return Forbid();

        var key = (TKey)JsonSerializer.Deserialize(stringKey, modelHandler.KeyType)!;

        await modelHandler.DeleteModel!(key);

        var pageState = this.GetPageState();
        var tableModel = await modelHandler.BuildTableModel();
        tableModel.Rows.Add(new TableRowContext<T, TKey>
        {
            Item = default!,
            ModelHandler = modelHandler,
            Key = key,
            TargetDisposition = OobTargetDisposition.Delete,
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }

    [HttpPost("{typeId}/{modelUI}/Edit")]
    public async Task<IActionResult> Edit(string typeId, ModelUI modelUI, string key)
    {
        var modelHandler = await _modelRegistry.GetModelHandler(typeId, modelUI);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var result = await GenericMethodInvoker.InvokeAsync<IActionResult>(
            this,
            nameof(_Edit),
            [modelHandler.ModelType, modelHandler.KeyType],
            key, modelHandler);
        return result!;
    }

    private async Task<IActionResult> _Edit<T, TKey>(string stringKey, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        if (!await IsAuthorized(modelHandler.TypeId, Operations.Read))
            return Forbid();
        if (!await IsAuthorized(modelHandler.TypeId, Operations.Update))
            return Forbid();

        var key = (TKey)JsonSerializer.Deserialize(stringKey, modelHandler.KeyType)!;
        var editingItem = await modelHandler.GetQueryable!()
            .Where(modelHandler.GetKeyPredicate(key))
            .SingleOrDefaultAsync();
        if (editingItem == null)
            return BadRequest($"Model with key '{stringKey}' not found.");
        var pageState = this.GetPageState();
        pageState.Set("Table", "EditingItem", editingItem);
        pageState.Set("Table", "EditingExistingRecord", true);

        var tableModel = await modelHandler.BuildTableModel();
        tableModel.Rows.Add(new TableRowContext<T, TKey>
        {
            Item = editingItem,
            ModelHandler = modelHandler,
            Key = key,
            TargetDisposition = OobTargetDisposition.OuterHtml,
            IsEditing = true,
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }


    [HttpPost("{typeId}/SetPage")]
    public async Task<IActionResult> SetPage(string typeId, int page)
    {
        var modelHandler = await _modelRegistry.GetModelHandler(typeId, ModelUI.Table);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var result = await GenericMethodInvoker.InvokeAsync<IActionResult>(
            this,
            nameof(_SetPage),
            [modelHandler.ModelType, modelHandler.KeyType],
            page, modelHandler);
        return result!;
    }

    private async Task<IActionResult> _SetPage<T, TKey>(int page, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        if (!await IsAuthorized(modelHandler.TypeId, Operations.Read))
            return Forbid();

        var pageState = this.GetPageState();
        var tableState = pageState.GetOrCreate<TableState>("Table", "State", () => new());
        tableState.Page = page;
        pageState.Set("Table", "State", tableState);
        var tableModel = await modelHandler.BuildTableModel();
        await _tableProvider.FetchPage(tableModel, modelHandler.GetQueryable!(), tableState);

        return _tableProvider.RefreshAllViews(tableModel);
    }

    [HttpPost("{typeId}/SetPageSize")]
    public async Task<IActionResult> SetPageSize(string typeId, int pageSize)
    {
        var modelHandler = await _modelRegistry.GetModelHandler(typeId, ModelUI.Table);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var result = await GenericMethodInvoker.InvokeAsync<IActionResult>(
            this,
            nameof(_SetPageSize),
            [modelHandler.ModelType, modelHandler.KeyType],
            pageSize, modelHandler);
        return result!;
    }

    private async Task<IActionResult> _SetPageSize<T, TKey>(int pageSize, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        if (!await IsAuthorized(modelHandler.TypeId, Operations.Read))
            return Forbid();

        var pageState = this.GetPageState();
        var tableState = pageState.GetOrCreate<TableState>("Table", "State", () => new());
        tableState.PageSize = pageSize;
        pageState.Set("Table", "State", tableState);
        var tableModel = await modelHandler.BuildTableModel();
        await _tableProvider.FetchPage(tableModel, modelHandler.GetQueryable!(), tableState);

        return _tableProvider.RefreshAllViews(tableModel);
    }

    [HttpPost("{typeId}/SetSort")]
    public async Task<IActionResult> SetSort(string typeId, string column, string direction)
    {
        var modelHandler = await _modelRegistry.GetModelHandler(typeId, ModelUI.Table);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var result = await GenericMethodInvoker.InvokeAsync<IActionResult>(
            this,
            nameof(_SetSort),
            [modelHandler.ModelType, modelHandler.KeyType],
            column, direction, modelHandler);
        return result!;
    }

    private async Task<IActionResult> _SetSort<T, TKey>(string column, string direction, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        if (!await IsAuthorized(modelHandler.TypeId, Operations.Read))
            return Forbid();

        var pageState = this.GetPageState();
        var tableState = pageState.GetOrCreate<TableState>("Table", "State", () => new());
        tableState.SortColumn = column;
        tableState.SortDirection = direction;
        pageState.Set("Table", "State", tableState);
        var tableModel = await modelHandler.BuildTableModel();
        await _tableProvider.FetchPage(tableModel, modelHandler.GetQueryable!(), tableState);

        return _tableProvider.RefreshAllViews(tableModel);
    }

    [HttpPost("{typeId}/{modelUI}/SetValue")]
    public async Task<IActionResult> SetValue(string typeId, ModelUI modelUI, string propertyName, string value)
    {
        var modelHandler = await _modelRegistry.GetModelHandler(typeId, modelUI);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var result = await GenericMethodInvoker.InvokeAsync<IActionResult>(
            this,
            nameof(_SetValue),
            [modelHandler.ModelType, modelHandler.KeyType],
            propertyName, value, modelHandler);
        return result!;
    }

    private async Task<IActionResult> _SetValue<T, TKey>(string propertyName, string value, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        var pageState = this.GetPageState();
        var editingExistingRecord = pageState.Get<bool>("Table", "EditingExistingRecord")!;
        if (editingExistingRecord)
        {
            if (!await IsAuthorized(modelHandler.TypeId, Operations.Update))
                return Forbid();
        }
        else
        {
            if (!await IsAuthorized(modelHandler.TypeId, Operations.Create))
                return Forbid();
        }

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
        var modelHandler = await _modelRegistry.GetModelHandler(typeId, ModelUI.Table);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var result = await GenericMethodInvoker.InvokeAsync<IActionResult>(
            this,
            nameof(_SetFilter),
            [modelHandler.ModelType, modelHandler.KeyType],
            column, filter, input, modelHandler);
        return result!;
    }

    private async Task<IActionResult> _SetFilter<T, TKey>(string column, string filter, int input, ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        if (!await IsAuthorized(modelHandler.TypeId, Operations.Read))
            return Forbid();

        var tableModel = await modelHandler.BuildTableModel();
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

    [HttpPost("{typeId}/{modelUI}/Create")]
    public async Task<IActionResult> Create(string typeId, ModelUI modelUI)
    {
        var modelHandler = await _modelRegistry.GetModelHandler(typeId, modelUI);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var result = await GenericMethodInvoker.InvokeAsync<IActionResult>(
            this,
            nameof(_Create),
            [modelHandler.ModelType, modelHandler.KeyType],
            modelHandler);
        return result!;
    }

    private async Task<IActionResult> _Create<T, TKey>(ModelHandler<T, TKey> modelHandler)
        where T : class, new()
    {
        if (!await IsAuthorized(modelHandler.TypeId, Operations.Create))
            return Forbid();

        var editingItem = new T();

        var pageState = this.GetPageState();
        pageState.Set("Table", "EditingItem", editingItem);
        pageState.Set("Table", "EditingExistingRecord", false);

        var tableModel = await modelHandler.BuildTableModel();
        tableModel.Rows.Add(new TableRowContext<T, TKey>
        {
            Item = editingItem,
            ModelHandler = modelHandler,
            TargetDisposition = OobTargetDisposition.AfterBegin,
            TargetSelector = "#table-body",
            StringKey = "new",
            IsEditing = true,
        });

        return _tableProvider.RefreshEditViews(tableModel);
    }

    [HttpPost("{typeId}/{modelUI}/ValueChanged")]
    public async Task<IActionResult> ValueChanged(string typeId, ModelUI modelUI, string propertyName, string value)
    {
        var modelHandler = await _modelRegistry.GetModelHandler(typeId, modelUI);
        if (modelHandler == null)
            return BadRequest($"Model handler for type '{typeId}' not found.");

        var result = await GenericMethodInvoker.InvokeAsync<IActionResult>(
            this,
            nameof(_ValueChanged),
            [modelHandler.ModelType, modelHandler.KeyType],
            propertyName, value, modelHandler);
        return result!;
    }

    private Task<IActionResult> _ValueChanged<T, TKey>(string propertyName, string value,
        ModelHandler<T, TKey> modelHandler)
        where T : class
    {
        throw new NotImplementedException("This method is not implemented yet.");
    }


    private async Task<bool> IsAuthorized(string typeId, string operation)
    {
        var requirement = _permissionRequirementFactory.ForOperation(typeId, operation);
        var result = await _authorizationService.AuthorizeAsync(User, null, requirement);
        return result.Succeeded;
    }
}