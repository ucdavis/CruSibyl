namespace CruSibyl.Web.Configuration;

public class AzureSubscription
{
    public string SubscriptionId { get; set; } = null!;
    public bool Enabled { get; set; } = true;
    public bool Default { get; set; }
}

public class AzureConfig
{
    public Dictionary<string, AzureSubscription> Subscriptions { get; set; } = new();
}
