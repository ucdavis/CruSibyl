using System.Text.Json;
using Htmx.Components.Authorization;
using Htmx.Components.Models;
using Htmx.Components.Services;
using Htmx.Components.Table;
using Htmx.Components.Utilities;
using Htmx.Components.ViewResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Operations = Htmx.Components.Constants.Authorization.Operations;

namespace Htmx.Components.Controllers;

[Route("Table")]
public class InputController : Controller
{
    private readonly ITableProvider _tableProvider;
    private readonly IModelRegistry _modelRegistry;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPermissionRequirementFactory _permissionRequirementFactory;

    public InputController(ITableProvider tableProvider, IModelRegistry modelRegistry,
        IAuthorizationService authorizationService, IPermissionRequirementFactory permissionRequirementFactory)
    {
        _tableProvider = tableProvider;
        _modelRegistry = modelRegistry;
        _authorizationService = authorizationService;
        _permissionRequirementFactory = permissionRequirementFactory;
    }


    [HttpPost("{typeId}/ValueChanged")]
    public async Task<IActionResult> ValueChanged(string typeId, string propertyName, string value)
    {
        var modelHandler = await _modelRegistry.GetModelHandler(typeId);
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