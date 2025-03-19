using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(EntityType), nameof(EntityId), nameof(TagId), IsUnique = true)]
public class TagMapping
{
    public int Id { get; set; }

    [Required]
    public int TagId { get; set; }

    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; } = null!;

    [Required]
    public EntityType EntityType { get; set; }

    public int EntityId { get; set; }
}
