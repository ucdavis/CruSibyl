# Tables

HTMX Components provides powerful table functionality with sorting, filtering, pagination, and inline editing capabilities.

## Basic Table Setup

### Model Configuration

Start by configuring your model handler with table settings:

```csharp
[ModelConfig("products")]
private void ConfigureProductModel(ModelHandlerBuilder<Product, int> builder)
{
    builder.WithKeySelector(p => p.Id)
           .WithQueryable(() => _context.Products)
           .WithTable(table =>
           {
               table.AddSelectorColumn(p => p.Name);
               table.AddSelectorColumn(p => p.Price);
               table.AddSelectorColumn(p => p.Category);
               table.AddSelectorColumn(p => p.CreatedDate);
           });
}
```

### Controller Action

Create a controller action that builds and returns a table model:

```csharp
public async Task<IActionResult> Index()
{
    var modelHandler = await _modelRegistry.GetModelHandler<Product, int>("products", ModelUI.Table);
    var tableModel = await modelHandler.BuildTableModelAndFetchPageAsync();
    return View(tableModel);
}
```

### View

Render the table using the Table component:

```html
@using Htmx.Components.Table.Models
@model ITableModel

<div class="container">
    <h1>Products</h1>
    @await Component.InvokeAsync("Table", Model)
</div>
```

## Column Types

### Selector Columns

Display data from model properties:

```csharp
table.AddSelectorColumn(p => p.Name, col => col
    .WithHeader("Product Name")
    .WithEditable()
    .WithFilter(FilterByName));

table.AddSelectorColumn(p => p.Price, col => col
    .WithHeader("Price ($)")
    .WithFilter(FilterByPriceRange));
```

### Display Columns

Show custom content or actions:

```csharp
table.AddDisplayColumn("Actions", col => col
    .WithActions((row, actions) =>
    {
        actions.AddAction(action => action
            .WithLabel("Edit")
            .WithIcon("fas fa-edit")
            .WithHxPost($"/Products/Edit/{row.Key}"));
            
        actions.AddAction(action => action
            .WithLabel("Delete")
            .WithIcon("fas fa-trash")
            .WithClass("text-red-600")
            .WithHxPost($"/Products/Delete/{row.Key}"));
    }));
```

## Filtering

### Basic Text Filters

Automatically enabled for selector columns:

```csharp
table.AddSelectorColumn(p => p.Name)
     .WithFilter((query, value) =>
         query.Where(p => p.Name.Contains(value)));
```

### Advanced Filter Syntax

The table supports advanced filtering with operators:

```
# Exact match
= "John Doe"

# Contains (default)
John

# Comparison operators
> 100
<= 50
!= "Active"

# Range filtering
between 10, 100

# Null checks
isnull
isnotnull

# String operations
startswith "Mr"
endswith ".com"
contains "test"

# Column references
> [Other Column]
between [Min Value], [Max Value]
```

### Custom Filters

Create specialized filters for complex scenarios:

```csharp
table.AddSelectorColumn(p => p.Status, col => col
    .WithFilter((query, value) =>
    {
        if (Enum.TryParse<ProductStatus>(value, out var status))
            return query.Where(p => p.Status == status);
        return query;
    }));
```

### Range Filters

Useful for dates and numeric values:

```csharp
table.AddSelectorColumn(p => p.CreatedDate, col => col
    .WithRangeFilter((query, fromDate, toDate) =>
    {
        var from = DateTime.Parse(fromDate);
        var to = DateTime.Parse(toDate);
        return query.Where(p => p.CreatedDate >= from && p.CreatedDate <= to);
    }));
```

## Sorting

### Automatic Sorting

Enabled by default for selector columns:

```csharp
table.AddSelectorColumn(p => p.Name); // Automatically sortable
```

## Pagination

Pagination is automatically handled by the table provider. Configure page size:

```csharp
// In your action
var tableState = pageState.GetOrCreate<TableState>("Table", "TableState", () => new TableState
{
    PageSize = 25 // Default page size
});
```

Users can change page size using the built-in pagination controls.

## CRUD Operations

### Enable CRUD

Configure create, read, update, and delete operations:

```csharp
builder.WithCreate(CreateProduct)
       .WithUpdate(UpdateProduct)
       .WithDelete(DeleteProduct)
       .WithTable(table =>
       {
           table.AddCrudDisplayColumn(); // Adds Edit/Delete buttons
           table.WithCrudActions();      // Adds Create button
       });
```

### CRUD Implementation

Implement the CRUD operations:

```csharp
private async Task<Result<Product>> CreateProduct(Product product)
{
    try
    {
        if (string.IsNullOrEmpty(product.Name))
            return Result.Error("Product name is required");
            
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return Result.Value(product);
    }
    catch (Exception ex)
    {
        return Result.Error("Failed to create product: {Error}", ex.Message);
    }
}

private async Task<Result<Product>> UpdateProduct(Product product)
{
    try
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
        return Result.Value(product);
    }
    catch (Exception ex)
    {
        return Result.Error("Failed to update product: {Error}", ex.Message);
    }
}

private async Task<Result> DeleteProduct(int productId)
{
    try
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null)
            return Result.Error("Product not found");
            
        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return Result.Ok("Product deleted successfully");
    }
    catch (Exception ex)
    {
        return Result.Error("Failed to delete product: {Error}", ex.Message);
    }
}
```

## Inline Editing

### Enable Inline Editing

Configure columns for inline editing:

```csharp
table.AddSelectorColumn(p => p.Name, col => col
    .WithEditable());

table.AddSelectorColumn(p => p.Category, col => col
    .WithEditable());
```

### Input Configuration

Define how fields are edited:

```csharp
builder.WithInput(p => p.Name, input => input
    .WithLabel("Product Name")
    .WithKind(InputKind.Text)
    .WithPlaceholder("Enter product name"));

builder.WithInput(p => p.Category, input => input
    .WithLabel("Category")
    .WithKind(InputKind.Select)
    .WithOptions(GetCategoryOptions()));

private List<KeyValuePair<string, string>> GetCategoryOptions()
{
    return new List<KeyValuePair<string, string>>
    {
        new("electronics", "Electronics"),
        new("clothing", "Clothing"),
        new("books", "Books")
    };
}
```

## Custom Table Views

### Override Table Templates

Customize table rendering by overriding view paths:

```csharp
builder.Services.AddHtmxComponents(options =>
{
    options.WithViewOverrides(views =>
    {
        views.Table.Table = "CustomTable";
        views.Table.Row = "CustomTableRow";
        views.Table.Cell = "CustomTableCell";
    });
});
```

### Custom Cell Rendering

Create custom cell templates:

```csharp
table.AddSelectorColumn(p => p.Status, col => col
    .WithCellPartial("_StatusCell"));
```

Create `Views/Shared/_StatusCell.cshtml`:

```html
@using Htmx.Components.Table.Models
@model TableCellPartialModel

@{
    var status = (ProductStatus)Model.Column.GetValue(Model.Row);
    var statusClass = status switch
    {
        ProductStatus.Active => "badge-success",
        ProductStatus.Inactive => "badge-error",
        _ => "badge-neutral"
    };
}

<span class="badge @statusClass">@status</span>
```

## Advanced Features

### Conditional Actions

Show different actions based on row data:

```csharp
table.AddDisplayColumn("Actions", col => col
    .WithActions((row, actions) =>
    {
        var product = (Product)row.Item;
        
        if (product.Status == ProductStatus.Active)
        {
            actions.AddAction(action => action
                .WithLabel("Deactivate")
                .WithIcon("fas fa-pause")
                .WithHxPost($"/Products/Deactivate/{row.Key}"));
        }
        else
        {
            actions.AddAction(action => action
                .WithLabel("Activate")
                .WithIcon("fas fa-play")
                .WithHxPost($"/Products/Activate/{row.Key}"));
        }
    }));
```

### Bulk Operations

Add table-level actions for bulk operations:

```csharp
table.WithActions((tableModel, actions) =>
{
    actions.AddAction(action => action
        .WithLabel("Export CSV")
        .WithIcon("fas fa-download")
        .WithHxGet($"/Products/ExportCsv"));
        
    actions.AddAction(action => action
        .WithLabel("Bulk Delete")
        .WithIcon("fas fa-trash")
        .WithClass("btn-error")
        .WithHxPost("/Products/BulkDelete"));
});
```

### Complex Filtering

Implement complex filtering scenarios:

```csharp
public async Task<IActionResult> FilterByCategory(string category)
{
    var modelHandler = await _modelRegistry.GetModelHandler<Product, int>("products", ModelUI.Table);
    var tableState = this.GetPageState().GetOrCreate<TableState>("Table", "TableState", () => new());
    
    // Apply custom filter
    tableState.Filters["Category"] = category;
    
    var tableModel = await modelHandler.BuildTableModelAndFetchPageAsync(tableState);
    return Ok(tableModel);
}
```

## Performance Optimization

### Efficient Queries

Optimize your queryables for performance:

```csharp
builder.WithQueryable(() => _context.Products
    .Include(p => p.Category)
    .AsNoTracking() // For read-only scenarios
    .AsSplitQuery()); // For complex includes
```

### Pagination Strategy

Use efficient pagination for large datasets:

```csharp
// Consider using cursor-based pagination for very large tables
private async Task<TableModel<Product, int>> GetProductsPage(int page, int pageSize)
{
    var skip = (page - 1) * pageSize;
    var products = await _context.Products
        .OrderBy(p => p.Id)
        .Skip(skip)
        .Take(pageSize)
        .ToListAsync();
        
    // Use the async builder for consistency
    var handler = await _modelRegistry.GetModelHandler<Product, int>("products", ModelUI.Table);
    var tableModel = await handler.BuildTableModelAsync();
    return tableModel;
}
```

## Best Practices

1. **Use Appropriate Column Types**: Choose selector columns for data, display columns for actions
2. **Implement Proper Error Handling**: Always return meaningful error messages from CRUD operations
3. **Optimize Queries**: Use Entity Framework efficiently with proper includes and no-tracking
4. **Validate Input**: Implement validation in your CRUD operations
5. **Consider Authorization**: Protect sensitive operations with proper authorization
6. **Progressive Enhancement**: Ensure tables work without JavaScript as a fallback

## Troubleshooting

### Table Not Loading

1. Check that the model handler is properly configured
2. Verify the queryable returns data
3. Ensure the Table component is invoked with correct model type

### Filtering Not Working

1. Verify filter functions are properly implemented
2. Check that columns are marked as filterable
3. Ensure filter syntax is correct

### CRUD Operations Failing

1. Check error handling in CRUD operations
2. Verify authorization for operations
3. Ensure proper model validation

### Performance Issues

1. Review query efficiency and includes
2. Consider pagination for large datasets
3. Implement proper indexing on filtered/sorted columns