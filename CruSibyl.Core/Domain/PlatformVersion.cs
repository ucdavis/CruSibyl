using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(PlatformId), nameof(Version), IsUnique = true)]
public class PlatformVersion
{
    public int Id { get; set; }

    [Required]
    public int PlatformId { get; set; }

    [ForeignKey(nameof(PlatformId))]
    public Platform Platform { get; set; } = null!;

    [Required, MaxLength(50)]
    public string Version { get; set; } = null!;

    public bool IsLTS { get; set; }
}
