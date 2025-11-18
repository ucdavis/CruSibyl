using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(Name), IsUnique = true)]
[Index(nameof(ResourceGroup))]
[Index(nameof(SubscriptionId))]
[Index(nameof(CreatedAt))]
public class App
{
    public int Id { get; set; }

    [Required, MaxLength(255)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Azure Resource Group (optional for on-prem apps)
    /// </summary>
    [MaxLength(255)]
    public string? ResourceGroup { get; set; }

    /// <summary>
    /// Azure Subscription ID (optional for on-prem apps)
    /// </summary>
    [MaxLength(255)]
    public string? SubscriptionId { get; set; }

    public int? RepoId { get; set; }

    [ForeignKey(nameof(RepoId))]
    public Repo? Repo { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Azure Resource ID for the App Service
    /// </summary>
    [MaxLength(500)]
    public string? ResourceId { get; set; }

    /// <summary>
    /// The primary URL/hostname for the app
    /// </summary>
    [MaxLength(500)]
    public string? DefaultHostName { get; set; }

    /// <summary>
    /// App Service Plan SKU (e.g., Basic, Standard, Premium)
    /// </summary>
    [MaxLength(100)]
    public string? Sku { get; set; }

    /// <summary>
    /// Runtime stack (e.g., .NET 8, Node 20, Python 3.11)
    /// </summary>
    [MaxLength(100)]
    public string? RuntimeStack { get; set; }

    /// <summary>
    /// App kind (e.g., app, functionapp, api)
    /// </summary>
    [MaxLength(50)]
    public string? Kind { get; set; }

    /// <summary>
    /// Current state of the app (e.g., Running, Stopped)
    /// </summary>
    [MaxLength(50)]
    public string? State { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? LastHealthCheckAt { get; set; }

    public bool IsEnabled { get; set; } = true;

    public List<WebJob> WebJobs { get; set; } = new();
}
