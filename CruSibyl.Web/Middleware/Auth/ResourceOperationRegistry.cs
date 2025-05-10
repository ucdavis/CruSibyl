using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using Htmx.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CruSibyl.Web.Middleware.Auth;

public class ResourceOperationRegistry : IResourceOperationRegistry
{
    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;

    // We're caching the resources and operations to reduce the number of queries generated.
    private static readonly string ResourcesCacheKey = "RegisteredResources";
    private static readonly string OperationsCacheKey = "RegisteredOperations";

    public ResourceOperationRegistry(AppDbContext dbContext, IMemoryCache memoryCache)
    {
        _dbContext = dbContext;
        _cache = memoryCache;
    }

    public async Task<Dictionary<string, Resource>> GetRegisteredResourcesAsync()
    {
        var resources = await _cache.GetOrCreateAsync(ResourcesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); // or your preferred duration
            return await _dbContext.Resources.AsNoTracking()
                .ToDictionaryAsync(r => r.Name, r => r, StringComparer.OrdinalIgnoreCase);
        });
        return resources!;
    }

    public async Task<Dictionary<string, Operation>> GetRegisteredOperationsAsync()
    {
        var operations = await _cache.GetOrCreateAsync(OperationsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); // or your preferred duration
            return await _dbContext.Operations.AsNoTracking()
                .ToDictionaryAsync(o => o.Name, o => o, StringComparer.OrdinalIgnoreCase);
        });
        return operations!;
    }

    public async Task Register(string resource, string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource, nameof(resource));
        ArgumentException.ThrowIfNullOrWhiteSpace(operation, nameof(operation));

        var saveChanges = false;
        var resources = await GetRegisteredResourcesAsync();
        if (!resources.ContainsKey(resource))
        {
            var resourceEntity = new Resource { Name = resource };
            _dbContext.Resources.Add(resourceEntity);
            resources[resource] = resourceEntity;
            saveChanges = true;
        }

        var operations = await GetRegisteredOperationsAsync();
        if (!operations.ContainsKey(operation))
        {
            var operationEntity = new Operation { Name = operation };
            _dbContext.Operations.Add(operationEntity);
            operations[operation] = operationEntity;
            saveChanges = true;
        }

        if (saveChanges)
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}