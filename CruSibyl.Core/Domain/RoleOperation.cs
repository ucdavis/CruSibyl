using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

public class RoleOperation
{
    [Key]
    public int Id { get; set; }

    public int ResourceId { get; set; }
    public Resource Resource { get; set; } = null!;
    public int OperationId { get; set; }
    public Operation Operation { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoleOperation>().HasIndex(a => new { a.ResourceId, a.OperationId, a.RoleId }).IsUnique();
    }
}
