namespace CruSibyl.Core.Models.Settings;

public class AzureSettings
{
    public Dictionary<string, AzureSubscription> Subscriptions { get; set; } = new();
    
    /// <summary>
    /// Maximum number of concurrent WebJob status queries to Azure API
    /// </summary>
    public int MaxConcurrentStatusQueries { get; set; } = 10;
}

public class AzureSubscription
{
    public string SubscriptionId { get; set; } = null!;
    public bool Enabled { get; set; } = true;
}
