using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;
public class RoleOperation
{
    [Key]
    public int Id { get; set; }

    [MaxLength(50)]
    [Required]
    public string Resource { get; set; } = "";

    [MaxLength(50)]
    [Required]
    public string Operation { get; set; } = "";

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoleOperation>().HasIndex(a => new { a.Resource, a.Operation, a.RoleId }).IsUnique();
        modelBuilder.Entity<RoleOperation>()
            .HasOne(p => p.Role)
            .WithMany(r => r.Operations)
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
