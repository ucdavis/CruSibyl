using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Htmx.Components.State;

public interface IGlobalStateManager
{
    void Load(string? encrypted);
    string Encrypted { get; }
    string InitialEncrypted { get; }
    T? Get<T>(string partition, string key);
    void Set<T>(string partition, string key, T value);
}


public class GlobalStateManager : IGlobalStateManager
{
    private readonly IDataProtector _protector;
    private const string MetaPartition = "__meta";
    private const string VersionKey = "__version";

    public Dictionary<string, Dictionary<string, string>> State { get; private set; } = new();

    public GlobalStateManager(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("GlobalState");
    }

    public void Load(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted))
        {
            State = new Dictionary<string, Dictionary<string, string>>();
            return;
        }

        var json = _protector.Unprotect(encrypted);
        State = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json)
                ?? new Dictionary<string, Dictionary<string, string>>();
    }

    public string Encrypted => _protector.Protect(JsonSerializer.Serialize(State));

    public string InitialEncrypted => _protector.Protect(
        JsonSerializer.Serialize(
            new Dictionary<string, Dictionary<string, string>>()
            {
                ["__meta"] = new Dictionary<string, string>
                {
                    ["__version"] = "1"
                }
            }));

    private Dictionary<string, string> GetPartition(string partition)
    {
        if (!State.TryGetValue(partition, out var values))
        {
            values = new Dictionary<string, string>();
            State[partition] = values;
        }
        return values;
    }

    private void BumpVersion()
    {
        var meta = GetPartition(MetaPartition);
        if (!meta.TryGetValue(VersionKey, out var versionStr) || !int.TryParse(versionStr, out var version))
        {
            version = 0;
        }
        meta[VersionKey] = (version + 1).ToString();
    }

    public T? Get<T>(string partition, string key)
    {
        var p = GetPartition(partition);
        if (!p.TryGetValue(key, out var value)) return default;

        return (T?)Convert.ChangeType(value, typeof(T));
    }

    public void Set<T>(string partition, string key, T value)
    {
        var p = GetPartition(partition);
        p[key] = value?.ToString() ?? string.Empty;
        BumpVersion();
    }
}