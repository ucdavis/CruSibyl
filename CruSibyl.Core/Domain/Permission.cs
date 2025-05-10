using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(RoleId))]
[Index(nameof(UserId))]
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
}

