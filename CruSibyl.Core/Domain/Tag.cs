using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(Name), IsUnique = true)]
public class Tag
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;

    public List<TagMapping> Mappings { get; set; } = new();
}
