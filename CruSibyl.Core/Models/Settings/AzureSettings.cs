namespace CruSibyl.Core.Models.Settings;

public class AzureSettings
{
    public Dictionary<string, AzureSubscription> Subscriptions { get; set; } = new();
}

public class AzureSubscription
{
    public string SubscriptionId { get; set; } = null!;
    public bool Enabled { get; set; } = true;
}
