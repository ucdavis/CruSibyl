namespace CruSibyl.Core.Domain
{
    using System.ComponentModel.DataAnnotations;
    using Microsoft.EntityFrameworkCore;

    [Index(nameof(Name), IsUnique = true)]
    public class Resource
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(50)]
        [Required]
        public string Name { get; set; } = "";

        public List<RoleOperation> Operations { get; set; } = new();
    }
}