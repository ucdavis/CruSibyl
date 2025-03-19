using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(PackageId), nameof(Version), IsUnique = true)]
public class PackageVersion
{
    public int Id { get; set; }

    [Required]
    public int PackageId { get; set; }

    [ForeignKey(nameof(PackageId))]
    public Package Package { get; set; } = null!;

    [Required, MaxLength(50)]
    public string Version { get; set; } = null!;

    public bool IsLatestMinor { get; set; }
    public bool IsLatestMajor { get; set; }
}
