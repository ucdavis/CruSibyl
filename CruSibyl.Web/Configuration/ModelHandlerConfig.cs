using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using Htmx.Components;
using Htmx.Components.Services;

namespace CruSibyl.Web.Configuration;

public static class ModelHandlerConfig
{
    public static Action<IModelRegistry> RegisterModels => registry =>
    {
        RegisterRepo(registry);
    };

    private static void RegisterRepo(IModelRegistry registry)
    {
        registry.Register<Repo, int>(nameof(Repo), (serviceProvider, builder) =>
        {
            var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
            builder
                .WithKeySelector(r => r.Id)
                .WithQueryable(() => dbContext.Repos)
                .WithCreate(async repo =>
                {
                    dbContext.Repos.Add(repo);
                    await dbContext.SaveChangesAsync();
                    return Htmx.Components.Models.Result.Ok();
                })
                .WithUpdate(async repo =>
                {
                    dbContext.Repos.Update(repo);
                    await dbContext.SaveChangesAsync();
                    return Htmx.Components.Models.Result.Ok();
                })
                .WithDelete(async id =>
                {
                    var repo = await dbContext.Repos.FindAsync(id);
                    if (repo != null)
                    {
                        dbContext.Repos.Remove(repo);
                        await dbContext.SaveChangesAsync();
                        return Htmx.Components.Models.Result.Ok();
                    }
                    return Htmx.Components.Models.Result.Error("Repo not found");
                })
                .WithInput(r => r.Name, config => config
                    .WithLabel("Name")
                    .WithPlaceholder("Enter repo name")
                    .WithCssClass("form-control"))
                .WithInput(r => r.Description, config => config
                    .WithLabel("Description")
                    .WithPlaceholder("Enter repo description")
                    .WithCssClass("form-control"))
                .WithTable(table => table
                    .WithCrudActions()
                    .AddSelectorColumn("Name", x => x.Name, config => config
                        .WithEditable()
                        .WithFilter((q, val) => q.Where(x => x.Name.Contains(val))))
                    .AddSelectorColumn("Description", x => x.Description!, config => config
                        .WithEditable())
                    .AddCrudDisplayColumn());
        });
    }
}