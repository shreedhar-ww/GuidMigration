using System.Text.Json;
using System.Text.Json.Nodes;

namespace GuidMigration.Services;

public class DocumentTransformer
{
    private readonly GuidRemapper _remapper;

    public DocumentTransformer(GuidRemapper remapper)
    {
        _remapper = remapper;
    }

    /// <summary>
    /// Register all old IDs upfront so parentId lookups succeed during transformation.
    /// Must be called BEFORE any Transform methods.
    /// </summary>
    public void RegisterAllIds(IEnumerable<string> allOldIds)
    {
        foreach (var oldId in allOldIds)
        {
            _remapper.GetOrCreateNewId(oldId);
        }
        Logger.Success($"Registered {_remapper.Count} GUID mappings.");
    }

    /// <summary>
    /// Transform a Classification document: new GUID, null arrays, zero counts.
    /// </summary>
    public (string NewKey, JsonObject Document)? TransformClassification(string oldId, JsonElement rawDoc)
    {
        try
        {
            var node = JsonNode.Parse(rawDoc.GetRawText())!.AsObject();

            // Remove the docId field injected by the N1QL query
            node.Remove("docId");

            var newId = _remapper.GetOrCreateNewId(oldId);
            node["id"] = newId;

            // Null out arrays
            node["microlearning"] = null;
            node["jobbing"] = null;
            node["tracebility"] = null;

            // Zero counts
            node["microLearningCount"] = 0;
            node["jobbingCount"] = 0;
            node["traceabilityCount"] = 0;

            var name = node["name"]?.GetValue<string>() ?? "unknown";
            Logger.Info($"  Classification '{name}': {oldId} -> {newId}");

            return (newId, node);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to transform Classification {oldId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Transform a SubClassification document: new GUID, remap parentId, null arrays, zero counts.
    /// </summary>
    public (string NewKey, JsonObject Document)? TransformSubClassification(string oldId, JsonElement rawDoc)
    {
        try
        {
            var node = JsonNode.Parse(rawDoc.GetRawText())!.AsObject();

            // Remove the docId field injected by the N1QL query
            node.Remove("docId");

            var newId = _remapper.GetOrCreateNewId(oldId);
            node["id"] = newId;

            // Remap parentId
            var oldParentId = node["parentId"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(oldParentId))
            {
                var newParentId = _remapper.TryGetNewId(oldParentId);
                if (newParentId != null)
                {
                    node["parentId"] = newParentId;
                }
                else
                {
                    Logger.Warn($"SubClassification '{node["name"]}': parentId {oldParentId} not found in mapping. Keeping original.");
                }
            }

            // Null out arrays
            node["microlearning"] = null;
            node["jobbing"] = null;
            node["tracebility"] = null;

            // Zero counts
            node["microLearningCount"] = 0;
            node["jobbingCount"] = 0;
            node["traceabilityCount"] = 0;

            var name = node["name"]?.GetValue<string>() ?? "unknown";
            Logger.Info($"  SubClassification '{name}': {oldId} -> {newId}, parentId: {oldParentId} -> {node["parentId"]}");

            return (newId, node);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to transform SubClassification {oldId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Transform a Hierarchy document: remap id and parentId to new GUIDs.
    /// </summary>
    public (string NewKey, JsonObject Document)? TransformHierarchy(string oldId, JsonElement rawDoc)
    {
        try
        {
            var node = JsonNode.Parse(rawDoc.GetRawText())!.AsObject();

            // Remove the docId field injected by the N1QL query
            node.Remove("docId");

            // The hierarchy id maps to the same GUID as the corresponding classification/subclassification
            var newId = _remapper.TryGetNewId(oldId);
            if (newId == null)
            {
                Logger.Warn($"Hierarchy doc {oldId}: not found in GUID mapping. Skipping.");
                return null;
            }

            node["id"] = newId;

            // Remap parentId if not null
            var oldParentId = node["parentId"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(oldParentId))
            {
                var newParentId = _remapper.TryGetNewId(oldParentId);
                if (newParentId != null)
                {
                    node["parentId"] = newParentId;
                }
                else
                {
                    Logger.Warn($"Hierarchy '{node["name"]}': parentId {oldParentId} not found in mapping. Keeping original.");
                }
            }

            var name = node["name"]?.GetValue<string>() ?? "unknown";
            Logger.Info($"  Hierarchy '{name}': {oldId} -> {newId}, parentId: {oldParentId} -> {node["parentId"]}");

            return (newId, node);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to transform Hierarchy {oldId}: {ex.Message}");
            return null;
        }
    }
}
