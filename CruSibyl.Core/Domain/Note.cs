using System.ComponentModel.DataAnnotations;

namespace CruSibyl.Core.Domain;

public class Note
{
    public int Id { get; set; }

    [Required]
    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<NoteMapping> Mappings { get; set; } = new();
}
