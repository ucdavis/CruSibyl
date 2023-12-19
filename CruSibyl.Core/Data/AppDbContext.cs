using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        User.OnModelCreating(builder);
        Role.OnModelCreating(builder);
        Permission.OnModelCreating(builder);
    }
}
