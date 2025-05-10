using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(Name), IsUnique = true)]
public class Role
{
    [Key]
    public int Id { get; set; }

    [MaxLength(50)]
    [Required]
    public string Name { get; set; } = "";

    public List<RoleOperation> Operations { get; set; } = new();

    public List<Permission> Permissions { get; set; } = new();

    public class Codes
    {
        public const string System = "System";
        public const string Admin = "Admin";
    }
}
