using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;
public class Role
{
    [Key]
    public int Id { get; set; }

    [MaxLength(50)]
    [Required]
    public string Name { get; set; } = "";

    public List<RoleOperation> Operations { get; set; } = new();

    public List<Permission> Permissions { get; set; } = new();

    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasIndex(a => a.Name).IsUnique();        
    }

    public class Codes
    {
        public const string System = "System";
        public const string Admin = "Admin";
    }
}
