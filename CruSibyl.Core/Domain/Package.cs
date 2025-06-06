using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CruSibyl.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(Name), nameof(PlatformId), IsUnique = true)]
[Index(nameof(ScanStatus))]
[Index(nameof(LastScannedAt))]
[Index(nameof(ScanNumber))]
public class Package
{
    public int Id { get; set; }

    [Required, MaxLength(255)]
    public string Name { get; set; } = null!;

    [Required]
    public int PlatformId { get; set; }

    [ForeignKey(nameof(PlatformId))]
    public Platform Platform { get; set; } = null!;

    public DateTime? LastScannedAt { get; set; }

    public ScanStatus? ScanStatus { get; set; }

    public string? ScanMessage { get; set; }

    public int? ScanNumber { get; set; }

    public List<PackageVersion> Versions { get; set; } = new();
}