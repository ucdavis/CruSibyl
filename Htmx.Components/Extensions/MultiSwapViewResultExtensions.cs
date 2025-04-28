using Htmx.Components.Models;
using Htmx.Components.Results;
using Htmx.Components.State;

namespace Htmx.Components.Extensions;

public static class MultiSwapViewResultExtensions
{
    /// <summary>
    /// Adds a GlobalState swap to the view result.
    /// </summary>
    public static MultiSwapViewResult WithGlobalStateOobContent(this MultiSwapViewResult viewResult, IGlobalStateManager globalStateManager)
    {
        if (viewResult == null) throw new ArgumentNullException(nameof(viewResult));
        if (globalStateManager == null) throw new ArgumentNullException(nameof(globalStateManager));

        var encryptedState = globalStateManager.Encrypted;

        var oob = new HtmxViewInfo
            {
                ViewName = "_GlobalStateHiddenInput",
                Model = encryptedState,
                TargetRelation = OobTargetRelation.OuterHtml
            };

        return viewResult.WithOobContent(oob);

    }
}