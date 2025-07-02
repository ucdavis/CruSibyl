# Extending the Framework

Htmx.Components is designed to be extensible at multiple levels. This guide covers the various extension points and how to implement custom functionality.

## Custom Model Handlers

Model handlers are the core abstraction for data management. You can extend them for specialized scenarios.

### Creating Custom Base Classes

```csharp
public abstract class AuditableModelHandler<T, TKey> : ModelHandler<T, TKey>
    where T : class, IAuditable, new()
{
    public AuditableModelHandler(
        ModelHandlerOptions<T, TKey> options, 
        ITableProvider tableProvider, 
        IPageState pageState) 
        : base(options, tableProvider, pageState)
    {
    }

    protected virtual void ApplyAuditInfo(T entity, string operation)
    {
        entity.ModifiedDate = DateTime.UtcNow;
        entity.ModifiedBy = GetCurrentUser();
        
        if (operation == "CREATE")
        {
            entity.CreatedDate = DateTime.UtcNow;
            entity.CreatedBy = GetCurrentUser();
        }
    }

    private string GetCurrentUser()
    {
        // Get current user from context
        return "current-user";
    }
}
```

### Specialized CRUD Operations

```csharp
public class SoftDeleteModelHandler<T, TKey> : ModelHandler<T, TKey>
    where T : class, ISoftDeletable, new()
{
    public SoftDeleteModelHandler(
        ModelHandlerOptions<T, TKey> options, 
        ITableProvider tableProvider, 
        IPageState pageState) 
        : base(options, tableProvider, pageState)
    {
        // Override delete to use soft delete
        ConfigureSoftDelete();
    }

    private void ConfigureSoftDelete()
    {
        DeleteModel = async (key) =>
        {
            var entity = await GetQueryable!()
                .Where(GetKeyPredicate(key))
                .FirstOrDefaultAsync();
                
            if (entity == null)
                return Result.Error("Entity not found");

            entity.IsDeleted = true;
            entity.DeletedDate = DateTime.UtcNow;
            
            // Save changes through your data context
            await SaveChangesAsync();
            
            return Result.Ok("Entity soft deleted");
        };
    }
}
```

## Custom Table Columns

Extend table functionality with specialized column types.

### Rich Content Columns

```csharp
public static class TableColumnExtensions
{
    public static TableColumnModelBuilder<T, TKey> WithImageColumn<T, TKey>(
        this TableColumnModelBuilder<T, TKey> builder,
        string imageUrlProperty,
        string altTextProperty = null)
        where T : class
    {
        return builder.WithCellPartial("_ImageCell")
                     .WithAttribute("data-image-url", imageUrlProperty)
                     .WithAttribute("data-alt-text", altTextProperty ?? "");
    }

    public static TableColumnModelBuilder<T, TKey> WithProgressBar<T, TKey>(
        this TableColumnModelBuilder<T, TKey> builder,
        int maxValue = 100)
        where T : class
    {
        return builder.WithCellPartial("_ProgressBarCell")
                     .WithAttribute("data-max-value", maxValue.ToString());
    }

    public static TableColumnModelBuilder<T, TKey> WithStatusBadge<T, TKey>(
        this TableColumnModelBuilder<T, TKey> builder,
        Dictionary<string, string> statusColors = null)
        where T : class
    {
        return builder.WithCellPartial("_StatusBadgeCell")
                     .WithAttribute("data-status-colors", 
                         JsonSerializer.Serialize(statusColors ?? new()));
    }
}
```

### Custom Cell Partial Views

Create `Views/Shared/Components/Table/_ImageCell.cshtml`:

```html
@using Htmx.Components.Table.Models
@model TableCellPartialModel

@{
    var value = Model.Column.GetValue(Model.Row);
    var imageUrl = value?.ToString();
    var altText = Model.Column.Attributes.GetValueOrDefault("data-alt-text", "Image");
}

@if (!string.IsNullOrEmpty(imageUrl))
{
    <div class="avatar">
        <div class="w-12 h-12 rounded">
            <img src="@imageUrl" alt="@altText" class="object-cover" />
        </div>
    </div>
}
else
{
    <div class="avatar placeholder">
        <div class="bg-neutral-focus text-neutral-content rounded w-12 h-12">
            <span class="text-xl">?</span>
        </div>
    </div>
}
```

### Complex Filter Columns

```csharp
public static class FilterExtensions
{
    public static TableColumnModelBuilder<T, TKey> WithMultiSelectFilter<T, TKey>(
        this TableColumnModelBuilder<T, TKey> builder,
        IEnumerable<SelectListItem> options)
        where T : class
    {
        return builder
            .WithFilterPartial("_MultiSelectFilter")
            .WithFilter((query, selectedValues) =>
            {
                if (string.IsNullOrEmpty(selectedValues))
                    return query;

                var values = JsonSerializer.Deserialize<string[]>(selectedValues);
                if (values?.Length > 0)
                {
                    // Apply multi-select filtering logic
                    return query.Where(BuildMultiSelectPredicate<T>(
                        builder.PropertyExpression, values));
                }
                return query;
            });
    }

    private static Expression<Func<T, bool>> BuildMultiSelectPredicate<T>(
        Expression<Func<T, object>> propertyExpression, 
        string[] values)
    {
        // Build dynamic LINQ expression for multi-select
        var parameter = propertyExpression.Parameters[0];
        var property = propertyExpression.Body;
        
        // Create: values.Contains(entity.Property)
        var containsMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(string));

        var valuesConstant = Expression.Constant(values);
        var containsCall = Expression.Call(containsMethod, valuesConstant, property);
        
        return Expression.Lambda<Func<T, bool>>(containsCall, parameter);
    }
}
```

## Custom View Components

Create reusable UI components that integrate with the HTMX workflow.

### Dashboard Widget Component

```csharp
public class DashboardWidgetViewComponent : ViewComponent
{
    private readonly IMetricsService _metricsService;

    public DashboardWidgetViewComponent(IMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    public async Task<IViewComponentResult> InvokeAsync(
        string widgetType, 
        object parameters = null)
    {
        var model = widgetType switch
        {
            "sales" => await _metricsService.GetSalesMetricsAsync(parameters),
            "users" => await _metricsService.GetUserMetricsAsync(parameters),
            "performance" => await _metricsService.GetPerformanceMetricsAsync(parameters),
            _ => throw new ArgumentException($"Unknown widget type: {widgetType}")
        };

        return View($"_{widgetType.Pascalize()}Widget", model);
    }
}
```

### Real-time Updates Component

```csharp
[Route("api/[controller]")]
public class LiveUpdatesController : Controller
{
    [HttpGet("status/{componentId}")]
    public async Task<IActionResult> GetStatus(string componentId)
    {
        var status = await GetCurrentStatusAsync(componentId);
        
        return ViewComponent("LiveStatus", new { 
            ComponentId = componentId, 
            Status = status 
        });
    }
}

public class LiveStatusViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string componentId, object status)
    {
        var model = new LiveStatusModel 
        { 
            ComponentId = componentId, 
            Status = status,
            LastUpdated = DateTime.UtcNow
        };
        
        return View(model);
    }
}
```

Create the view `Views/Shared/Components/LiveStatus/Default.cshtml`:

```html
@model LiveStatusModel

<div id="status-@Model.ComponentId" 
     hx-get="@Url.Action("GetStatus", "LiveUpdates", new { componentId = Model.ComponentId })"
     hx-trigger="every 5s"
     hx-swap="outerHTML">
    
    <div class="card">
        <div class="card-body">
            <h3 class="card-title">@Model.ComponentId Status</h3>
            <p class="text-sm text-gray-500">
                Last updated: @Model.LastUpdated.ToString("HH:mm:ss")
            </p>
            <div class="status-content">
                @Html.Raw(Model.Status)
            </div>
        </div>
    </div>
</div>
```

## Custom Result Filters

Create specialized filters for complex HTMX interactions.

### Notification Filter

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class ShowNotificationAttribute : Attribute
{
    public string Message { get; set; }
    public string Type { get; set; } = "info"; // info, success, warning, error
    
    public ShowNotificationAttribute(string message)
    {
        Message = message;
    }
}

public class NotificationFilter : OobResultFilterBase<ShowNotificationAttribute>
{
    protected override Task UpdateMultiSwapViewResultAsync(
        ShowNotificationAttribute attribute, 
        MultiSwapViewResult multiSwapViewResult, 
        ResultExecutingContext context)
    {
        var notification = new NotificationModel
        {
            Message = attribute.Message,
            Type = attribute.Type,
            Id = Guid.NewGuid().ToString("N")[..8]
        };

        multiSwapViewResult.WithOobContent("_Notification", notification);
        return Task.CompletedTask;
    }

    protected override Task<string?> GetViewNameForNonHtmxRequest(
        ShowNotificationAttribute attribute, 
        ControllerActionDescriptor cad)
    {
        // For non-HTMX requests, add to TempData
        var controller = cad.ControllerTypeInfo.Name.Replace("Controller", "");
        return Task.FromResult<string?>($"{controller}/{cad.ActionName}");
    }
}
```

### Progress Tracking Filter

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class TrackProgressAttribute : Attribute
{
    public string ProgressKey { get; set; }
    
    public TrackProgressAttribute(string progressKey)
    {
        ProgressKey = progressKey;
    }
}

public class ProgressTrackingFilter : IAsyncActionFilter
{
    private readonly IProgressTracker _progressTracker;

    public ProgressTrackingFilter(IProgressTracker progressTracker)
    {
        _progressTracker = progressTracker;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, 
        ActionExecutionDelegate next)
    {
        var attribute = context.ActionDescriptor
            .GetCustomAttribute<TrackProgressAttribute>();

        if (attribute != null)
        {
            await _progressTracker.StartAsync(attribute.ProgressKey);
            
            try
            {
                var result = await next();
                await _progressTracker.CompleteAsync(attribute.ProgressKey);
            }
            catch (Exception ex)
            {
                await _progressTracker.FailAsync(attribute.ProgressKey, ex.Message);
                throw;
            }
        }
        else
        {
            await next();
        }
    }
}
```

## Custom Input Types

Extend the input system with specialized controls.

### Rich Text Editor Input

```csharp
public static class InputExtensions
{
    public static InputModelBuilder<T, string> AsRichText<T>(
        this InputModelBuilder<T, string> builder,
        RichTextOptions options = null)
        where T : class
    {
        options ??= new RichTextOptions();
        
        return builder
            .WithKind(InputKind.RichText)
            .WithAttribute("data-toolbar", string.Join(",", options.Toolbar))
            .WithAttribute("data-height", options.Height.ToString())
            .WithCssClass($"rich-text-editor {builder.CssClass}".Trim());
    }

    public static InputModelBuilder<T, string> AsCodeEditor<T>(
        this InputModelBuilder<T, string> builder,
        string language = "javascript",
        string theme = "vs-dark")
        where T : class
    {
        return builder
            .WithKind(InputKind.CodeEditor)
            .WithAttribute("data-language", language)
            .WithAttribute("data-theme", theme)
            .WithCssClass($"code-editor {builder.CssClass}".Trim());
    }
}

public enum InputKind
{
    // ... existing kinds
    RichText,
    CodeEditor,
    FileUpload,
    ColorPicker,
    DateRange
}

public class RichTextOptions
{
    public string[] Toolbar { get; set; } = { "bold", "italic", "underline", "|", "link", "image" };
    public int Height { get; set; } = 200;
    public bool AllowImages { get; set; } = true;
    public bool AllowLinks { get; set; } = true;
}
```

Update the `_Input.cshtml` partial to handle new input types:

```html
@case InputKind.RichText:
{
    <div class="rich-text-container" 
         data-toolbar="@Model.Attributes.GetValueOrDefault("data-toolbar", "")"
         data-height="@Model.Attributes.GetValueOrDefault("data-height", "200")">
        <textarea name="value" id="@(id)" 
                  class="rich-text-editor @Model.CssClass" 
                  hx-post="@Url.Action("SetValue", "Form", routeValues)" 
                  hx-trigger="blur" 
                  hx-vals='{"propertyName": "@Model.PropName"}'>@Html.Encode(Model.Value?.ToString())</textarea>
    </div>
    break;
}

@case InputKind.CodeEditor:
{
    <div class="code-editor-container"
         data-language="@Model.Attributes.GetValueOrDefault("data-language", "javascript")"
         data-theme="@Model.Attributes.GetValueOrDefault("data-theme", "vs-dark")">
        <textarea name="value" id="@(id)" 
                  class="code-editor @Model.CssClass" 
                  hx-post="@Url.Action("SetValue", "Form", routeValues)" 
                  hx-trigger="blur" 
                  hx-vals='{"propertyName": "@Model.PropName"}'>@Html.Encode(Model.Value?.ToString())</textarea>
    </div>
    break;
}
```

## Custom Middleware

Create middleware that integrates with the HTMX workflow.

### Request Logging Middleware

```csharp
public class HtmxRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HtmxRequestLoggingMiddleware> _logger;

    public HtmxRequestLoggingMiddleware(
        RequestDelegate next, 
        ILogger<HtmxRequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.IsHtmx())
        {
            var trigger = context.Request.Headers["HX-Trigger"].FirstOrDefault();
            var target = context.Request.Headers["HX-Target"].FirstOrDefault();
            
            _logger.LogInformation(
                "HTMX Request: {Method} {Path} | Trigger: {Trigger} | Target: {Target}",
                context.Request.Method,
                context.Request.Path,
                trigger ?? "none",
                target ?? "none");
        }

        await _next(context);
    }
}
```

### Performance Monitoring Middleware

```csharp
public class HtmxPerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMetricsCollector _metrics;

    public HtmxPerformanceMiddleware(
        RequestDelegate next, 
        IMetricsCollector metrics)
    {
        _next = next;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            if (context.Request.IsHtmx())
            {
                await _metrics.RecordHtmxRequestAsync(new HtmxRequestMetrics
                {
                    Duration = stopwatch.Elapsed,
                    Path = context.Request.Path,
                    Method = context.Request.Method,
                    StatusCode = context.Response.StatusCode,
                    Trigger = context.Request.Headers["HX-Trigger"].FirstOrDefault(),
                    Target = context.Request.Headers["HX-Target"].FirstOrDefault()
                });
            }
        }
    }
}
```

## Integration with External Libraries

### Entity Framework Integration

```csharp
public static class ModelHandlerExtensions
{
    public static ModelHandlerBuilder<T, TKey> WithEfCore<T, TKey>(
        this ModelHandlerBuilder<T, TKey> builder,
        DbContext context,
        Expression<Func<DbContext, DbSet<T>>> dbSetSelector = null)
        where T : class, new()
    {
        dbSetSelector ??= ctx => ctx.Set<T>();
        var dbSetFunc = dbSetSelector.Compile();

        return builder.WithQueryable(() => dbSetFunc(context).AsQueryable())
                     .WithCreate(async entity =>
                     {
                         try
                         {
                             context.Add(entity);
                             await context.SaveChangesAsync();
                             return Result.Value(entity);
                         }
                         catch (Exception ex)
                         {
                             return Result.Error("Failed to create entity: {Error}", ex.Message);
                         }
                     })
                     .WithUpdate(async entity =>
                     {
                         try
                         {
                             context.Update(entity);
                             await context.SaveChangesAsync();
                             return Result.Value(entity);
                         }
                         catch (Exception ex)
                         {
                             return Result.Error("Failed to update entity: {Error}", ex.Message);
                         }
                     });
    }
}
```

### SignalR Integration

```csharp
public class SignalRNotificationFilter : IAsyncResultFilter
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationFilter(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task OnResultExecutionAsync(
        ResultExecutingContext context, 
        ResultExecutionDelegate next)
    {
        var result = await next();

        if (context.Result is MultiSwapViewResult multiSwap && 
            context.HttpContext.Request.IsHtmx())
        {
            // Notify other clients about the change
            var notification = new
            {
                Action = context.ActionDescriptor.DisplayName,
                Timestamp = DateTime.UtcNow,
                User = context.HttpContext.User.Identity?.Name
            };

            await _hubContext.Clients.Others.SendAsync("HtmxUpdate", notification);
        }
    }
}
```

## Related Documentation

- **[Design Choices](design-choices.md)**: Understand the architectural decisions that enable extensibility
- **[Architecture Overview](architecture.md)**: Complete framework architecture and extension points
- **[Component Architecture](component-architecture.md)**: Self-contained component pattern for custom components

This comprehensive extension system allows you to adapt Htmx.Components to virtually any use case while maintaining the framework's core principles and patterns.