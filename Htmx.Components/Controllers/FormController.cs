using Htmx.Components.Authorization;
using Htmx.Components.Services;
using Htmx.Components.Table;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Htmx.Components.Controllers;

[Route("Form")]
public partial class FormController : Controller
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

    private async Task<bool> IsAuthorized(string typeId, string operation)
    {
        var requirement = _permissionRequirementFactory.ForOperation(typeId, operation);
        var result = await _authorizationService.AuthorizeAsync(User, null, requirement);
        return result.Succeeded;
    }
}