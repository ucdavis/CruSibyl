using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(ManifestId), nameof(PackageVersionId), IsUnique = true)]
public class Dependency
{
    public int Id { get; set; }

    [Required]
    public int ManifestId { get; set; }

    [ForeignKey(nameof(ManifestId))]
    public Manifest Manifest { get; set; } = null!;

    [Required]
    public int PackageVersionId { get; set; }

    [ForeignKey(nameof(PackageVersionId))]
    public PackageVersion PackageVersion { get; set; } = null!;

    public bool? IsDevDependency { get; set; } = null;
}
