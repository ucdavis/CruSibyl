namespace CruSibyl.Core.Models.Settings;

public class AzureSettings
{
    public string SubscriptionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Maximum number of concurrent WebJob status queries to Azure API
    /// </summary>
    public int MaxConcurrentStatusQueries { get; set; } = 10;
}
