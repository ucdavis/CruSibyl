using CruSibyl.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Data;

public sealed class AppDbContextSqlite : AppDbContext
{
    public AppDbContextSqlite(DbContextOptions<AppDbContextSqlite> options) : base(options)
    {
    }
}

public sealed class AppDbContextSqlServer : AppDbContext
{
    public AppDbContextSqlServer(DbContextOptions<AppDbContextSqlServer> options) : base(options)
    {
    }
}

public abstract class AppDbContext : DbContext
{
    protected AppDbContext(DbContextOptions options) : base(options)
    {
    }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<Permission> Permissions { get; set; }
    public virtual DbSet<Note> Notes { get; set; }
    public virtual DbSet<NoteMapping> NoteMappings { get; set; }
    public virtual DbSet<Package> Packages { get; set; }
    public virtual DbSet<PackageVersion> PackageVersions { get; set; }
    public virtual DbSet<Platform> Platforms { get; set; }
    public virtual DbSet<PlatformVersion> PlatformVersions { get; set; }
    public virtual DbSet<Manifest> Manifests { get; set; }
    public virtual DbSet<Dependency> Dependencies { get; set; }
    public virtual DbSet<Repo> Repos { get; set; }
    public virtual DbSet<Tag> Tags { get; set; }
    public virtual DbSet<TagMapping> TagMappings { get; set; }
    public virtual DbSet<RoleOperation> RoleOperations { get; set; }
    public virtual DbSet<Resource> Resources { get; set; }
    public virtual DbSet<Operation> Operations { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Set DeleteBehavior.Restrict for all required relationships
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                if (!foreignKey.IsOwnership && foreignKey.IsRequired)
                {
                    foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
                }
            }
        }

        RoleOperation.OnModelCreating(builder);
    }
}

// This is a workaround for the fact that IDbContextFactory<T> is not covariant
public class DbContextFactoryAdapter<TConcrete> : IDbContextFactory<AppDbContext>
    where TConcrete : AppDbContext
{
    private readonly IDbContextFactory<TConcrete> _inner;
    public DbContextFactoryAdapter(IDbContextFactory<TConcrete> inner) => _inner = inner;
    public AppDbContext CreateDbContext() => _inner.CreateDbContext();
}