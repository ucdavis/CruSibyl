﻿using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(Iam), IsUnique = true)]
[Index(nameof(Email))]
public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = "";

    [Required]
    [MaxLength(50)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = "";

    [Required]
    [MaxLength(300)]
    [EmailAddress]
    public string Email { get; set; } = "";

    [MaxLength(10)]
    public string Iam { get; set; } = "";

    [MaxLength(20)]
    public string Kerberos { get; set; } = "";

    [MaxLength(20)] //It probably isn't this long....
    public string MothraId { get; set; } = "";

    [Display(Name = "Name")]
    public string Name => FirstName + " " + LastName;

    [JsonIgnore]
    public List<Permission> Permissions { get; set; } = new();
}
