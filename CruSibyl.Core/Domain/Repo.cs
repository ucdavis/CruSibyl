using System.ComponentModel.DataAnnotations;
using CruSibyl.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(Name), IsUnique = true)]
[Index(nameof(ScanStatus))]
[Index(nameof(LastScannedAt))]
[Index(nameof(ScanNumber))]
public class Repo
{
    public int Id { get; set; }

    [Required, MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime? LastScannedAt { get; set; }

    public ScanStatus? ScanStatus { get; set; }

    public string? ScanMessage { get; set; }

    public int? ScanNumber { get; set; }


    public List<Manifest> Manifests { get; set; } = new();
}