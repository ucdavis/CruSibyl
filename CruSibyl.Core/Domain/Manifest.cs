using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(RepoId), nameof(FilePath), IsUnique = true)]
public class Manifest
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int RepoId { get; set; }

    [ForeignKey(nameof(RepoId))]
    public Repo Repo { get; set; } = null!;

    [Required]
    public int PlatformVersionId { get; set; }

    [ForeignKey(nameof(PlatformVersionId))]
    public PlatformVersion PlatformVersion { get; set; } = null!;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = null!;

    public List<Dependency> Dependencies { get; set; } = new();
}