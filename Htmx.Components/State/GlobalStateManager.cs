using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Htmx.Components.State;

public interface IGlobalStateManager
{
    void Load(string? encrypted);
    string Encrypted { get; }
    T? Get<T>(string partition, string key);
    void Set<T>(string partition, string key, T value);
    void ClearKey(string partition, string key);
    void ClearPartition(string partition);
    bool IsDirty { get; }
}


public class GlobalStateManager : IGlobalStateManager
{
    private readonly IDataProtector _protector;
    private const string MetaPartition = "__meta";
    private const string VersionKey = "__version";
    private int _version = 0;

    public Dictionary<string, Dictionary<string, string>> State { get; private set; }

    public GlobalStateManager(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("GlobalState");
        State = InitialState;
    }

    private static Dictionary<string, Dictionary<string, string>> InitialState => new()
    {
        [MetaPartition] = new Dictionary<string, string>
        {
            [VersionKey] = "0"
        }
    };

    public void Load(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted))
        {
            State = InitialState;
        }
        else
        {
            var json = _protector.Unprotect(encrypted);
            State = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json)
                    ?? new Dictionary<string, Dictionary<string, string>>();
        }
        _version = Get<int>(MetaPartition, VersionKey);
    }

    public string Encrypted => _protector.Protect(JsonSerializer.Serialize(State));

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

        return JsonSerializer.Deserialize<T>(value);
    }

    public void Set<T>(string partition, string key, T value)
    {
        var p = GetPartition(partition);
        p[key] = JsonSerializer.Serialize(value);
        BumpVersion();
    }

    public void ClearKey(string partition, string key)
    {
        var p = GetPartition(partition);
        if (p.Remove(key))
        {
            BumpVersion();
        }
    }

    public void ClearPartition(string partition)
    {
        if (State.Remove(partition))
        {
            BumpVersion();
        }
    }

    public bool IsDirty => _version != Get<int>(MetaPartition, VersionKey);
}