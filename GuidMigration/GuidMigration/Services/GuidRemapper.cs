namespace GuidMigration.Services;

public class GuidRemapper
{
    private readonly Dictionary<string, string> _mapping = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Generates a new GUID for the given old ID and stores the mapping.
    /// If already mapped, returns the existing new GUID.
    /// </summary>
    public string GetOrCreateNewId(string oldId)
    {
        if (_mapping.TryGetValue(oldId, out var existingNewId))
            return existingNewId;

        var newId = Guid.NewGuid().ToString();
        _mapping[oldId] = newId;
        return newId;
    }

    /// <summary>
    /// Looks up a previously mapped ID. Returns null if not found.
    /// </summary>
    public string? TryGetNewId(string oldId)
    {
        return _mapping.TryGetValue(oldId, out var newId) ? newId : null;
    }

    /// <summary>
    /// Returns the full mapping for logging/verification.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAllMappings() => _mapping;

    public int Count => _mapping.Count;
}
