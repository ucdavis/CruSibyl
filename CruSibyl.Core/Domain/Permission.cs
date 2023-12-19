using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;
public class Permission
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int RoleId { get; set; }

    [Required]
    public int UserId { get; set; }

    public Role Role { get; set; } = null!;

    public User User { get; set; } = null!;

    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Permission>().HasIndex(a => a.RoleId);
        modelBuilder.Entity<Permission>().HasIndex(a => a.UserId);
    }
}

