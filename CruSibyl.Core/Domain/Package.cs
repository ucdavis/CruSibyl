using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(Name), nameof(PlatformId), IsUnique = true)]
public class Package
{
    public int Id { get; set; }

    [Required, MaxLength(255)]
    public string Name { get; set; } = null!;

    [Required]
    public int PlatformId { get; set; }

    [ForeignKey(nameof(PlatformId))]
    public Platform Platform { get; set; } = null!;

    public List<PackageVersion> Versions { get; set; } = new();
}