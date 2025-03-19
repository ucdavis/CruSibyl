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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        User.OnModelCreating(builder);
        Role.OnModelCreating(builder);
        Permission.OnModelCreating(builder);
    }
}
