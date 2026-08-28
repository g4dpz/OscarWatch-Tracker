using System.Text.Json;
using System.Text.Json.Serialization;

namespace OscarWatch.Core.Dxcc;

public sealed class DxccEntityInfo
{
    public required int Dxcc { get; init; }
    public required string Country { get; init; }
}

public sealed class DxccEntityMap
{
    private readonly Dictionary<string, DxccEntityInfo> _byPrimaryPrefix;
    private readonly Dictionary<string, string> _waeParentByName;

    private static readonly Dictionary<string, string> DefaultWaeParents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sicily"] = "Italy",
        ["African Italy"] = "Italy",
        ["IG9"] = "Italy",
        ["IH9"] = "Italy"
    };

    public DxccEntityMap(
        IReadOnlyDictionary<string, DxccEntityInfo> byPrimaryPrefix,
        IReadOnlyDictionary<string, string>? waeParentByName = null)
    {
        _byPrimaryPrefix = new Dictionary<string, DxccEntityInfo>(byPrimaryPrefix, StringComparer.OrdinalIgnoreCase);
        _waeParentByName = waeParentByName is null
            ? new Dictionary<string, string>(DefaultWaeParents, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(waeParentByName, StringComparer.OrdinalIgnoreCase);
    }

    public static DxccEntityMap LoadFromJsonFile(string path) =>
        LoadFromJson(File.ReadAllText(path));

    public static DxccEntityMap LoadFromJson(string json)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, EntityDto>>(json, JsonOptions)
            ?? throw new InvalidOperationException("DXCC entity map JSON was empty.");

        var map = new Dictionary<string, DxccEntityInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var (prefix, dto) in raw)
        {
            if (string.IsNullOrWhiteSpace(prefix) || dto.Dxcc <= 0 || string.IsNullOrWhiteSpace(dto.Country))
                continue;

            map[prefix.Trim()] = new DxccEntityInfo
            {
                Dxcc = dto.Dxcc,
                Country = dto.Country.Trim()
            };
        }

        if (map.Count == 0)
            throw new InvalidOperationException("DXCC entity map contained no entities.");

        return new DxccEntityMap(map);
    }

    public bool TryResolve(CtyEntity entity, out DxccEntityInfo info)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.IsWaeOnly || _waeParentByName.ContainsKey(entity.Name))
        {
            if (_waeParentByName.TryGetValue(entity.Name, out var parentName))
            {
                foreach (var candidate in _byPrimaryPrefix.Values)
                {
                    if (string.Equals(candidate.Country, parentName, StringComparison.OrdinalIgnoreCase))
                    {
                        info = candidate;
                        return true;
                    }
                }
            }

            // Fall through: try primary prefix anyway (Sardinia is a real DXCC entity).
        }

        if (_byPrimaryPrefix.TryGetValue(entity.PrimaryPrefix, out info!))
            return true;

        info = null!;
        return false;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private sealed class EntityDto
    {
        [JsonPropertyName("dxcc")]
        public int Dxcc { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; } = "";
    }
}
