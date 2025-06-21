# Basic Usage

This guide covers the fundamental patterns for using HTMX Components in your application.

## Core Concepts

HTMX Components is built around several key concepts:

- **View Components**: Server-side rendered components that handle their own state
- **Model Handlers**: Configuration objects that define how data models are displayed and manipulated
- **Page State**: Server-side state management that persists across HTMX requests
- **Out-of-Band Updates**: Efficient partial page updates without full page reloads

## View Components

HTMX Components provides several built-in view components:

### Table Component

Renders interactive data tables with sorting, filtering, and pagination:

```html
@await Component.InvokeAsync("Table", tableModel)
```

### NavBar Component

Renders navigation menus based on controller attributes or programmatic configuration:

```html
@await Component.InvokeAsync("NavBar")
```

### AuthStatus Component

Shows current authentication status and user information:

```html
@await Component.InvokeAsync("AuthStatus")
```

## Model Handlers

Model handlers define how your data models are presented and manipulated. They're configured using the fluent builder pattern:

```csharp
[ModelConfig("products")]
private void ConfigureProductModel(ModelHandlerBuilder<Product, int> builder)
{
    builder.WithKeySelector(p => p.Id)
           .WithQueryable(() => _context.Products)
           .WithCreate(CreateProduct)
           .WithUpdate(UpdateProduct)
           .WithDelete(DeleteProduct)
           .WithTable(table =>
           {
               table.AddSelectorColumn(p => p.Name)
                    .WithEditable();
               table.AddSelectorColumn(p => p.Price)
                    .WithFilter(FilterByPrice);
               table.AddCrudDisplayColumn();
           });
}
```

### Key Components

- **Key Selector**: Defines the primary key for the model
- **Queryable**: Provides the data source (usually Entity Framework)
- **CRUD Operations**: Define create, update, and delete operations
- **Table Configuration**: Specifies how the model appears in tables

## Page State Management

Page state allows you to maintain server-side state across HTMX requests:

```csharp
public IActionResult MyAction()
{
    var pageState = this.GetPageState();
    
    // Get or create state
    var userPreferences = pageState.GetOrCreate<UserPreferences>(
        "user", "preferences", () => new UserPreferences());
    
    // Update state
    pageState.Set("user", "preferences", userPreferences);
    
    return Ok();
}
```

### Page State Features

- **Encrypted**: State is encrypted and sent to the client
- **Partitioned**: State is organized into logical partitions
- **Automatic**: State is automatically included in HTMX responses

## Working with Forms

HTMX Components provides seamless form handling with automatic state management:

### Input Models

Define how form fields are rendered:

```csharp
builder.WithInput(p => p.Name, input => input
    .WithLabel("Product Name")
    .WithPlaceholder("Enter product name")
    .WithKind(InputKind.Text));

builder.WithInput(p => p.Category, input => input
    .WithLabel("Category")
    .WithKind(InputKind.Select)
    .WithOptions(GetCategoryOptions()));
```

### Form Processing

Forms are automatically processed using the built-in FormController:

```html
<!-- This form will automatically save to the configured model handler -->
<form hx-post="/Form/products/Table/Save">
    @await Html.PartialAsync("_Input", nameInputModel)
    @await Html.PartialAsync("_Input", categoryInputModel)
    <button type="submit">Save</button>
</form>
```

## Error Handling

HTMX Components uses a `Result<T>` pattern for error handling:

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
```

## Filtering and Sorting

Tables support advanced filtering and sorting capabilities:

### Custom Filters

```csharp
table.AddSelectorColumn(p => p.Price)
     .WithFilter((query, filterValue) =>
     {
         if (decimal.TryParse(filterValue, out var price))
             return query.Where(p => p.Price <= price);
         return query;
     });
```

### Range Filters

```csharp
table.AddSelectorColumn(p => p.CreatedDate)
     .WithRangeFilter((query, fromDate, toDate) =>
     {
         var from = DateTime.Parse(fromDate);
         var to = DateTime.Parse(toDate);
         return query.Where(p => p.CreatedDate >= from && p.CreatedDate <= to);
     });
```

## Validation

Implement validation in your model handlers:

```csharp
private async Task<Result<Product>> ValidateAndCreateProduct(Product product)
{
    var validator = new ProductValidator();
    var validationResult = await validator.ValidateAsync(product);
    
    if (!validationResult.IsValid)
    {
        var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
        return Result.Error("Validation failed: {Errors}", errors);
    }
    
    return await CreateProduct(product);
}
```

## Best Practices

1. **Keep Controllers Thin**: Use model handlers for business logic
2. **Use Page State Sparingly**: Only store what's necessary between requests
3. **Implement Proper Error Handling**: Always return meaningful error messages
4. **Leverage Authorization**: Use the built-in authorization system for security
5. **Optimize Queries**: Use Entity Framework efficiently in your queryables

## Next Steps

- [Learn about navigation setup](navigation.md)
- [Explore advanced table features](tables.md)
- [Implement authentication](authentication.md)
- [Configure authorization](authorization.md)