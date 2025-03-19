using System.ComponentModel.DataAnnotations;

namespace CruSibyl.Core.Domain;

public class Repo
{
    public int Id { get; set; }

    [Required, MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public List<Manifest> Manifests { get; set; } = new();
}