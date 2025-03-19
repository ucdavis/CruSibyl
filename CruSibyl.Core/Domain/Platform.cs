using System.ComponentModel.DataAnnotations;

namespace CruSibyl.Core.Domain;

public class Platform
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = null!;

    public List<PlatformVersion> Versions { get; set; } = new();
}

