using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(EntityType), nameof(EntityId), nameof(NoteId), IsUnique = true)]
public class NoteMapping
{
    public int Id { get; set; }

    [Required]
    public int NoteId { get; set; }

    [ForeignKey(nameof(NoteId))]
    public Note Note { get; set; } = null!;

    [Required]
    public EntityType EntityType { get; set; }

    public int EntityId { get; set; }
}
