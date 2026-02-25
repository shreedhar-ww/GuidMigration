using System.Text.Json;
using Couchbase;
using GuidMigration.Models;
using Newtonsoft.Json.Linq;

namespace GuidMigration.Services;

public class DataFetcher
{
    private const int IdBatchSize = 100;

    /// <summary>
    /// Fetch all Hierarchy documents from source where companyId matches.
    /// </summary>
    public async Task<List<(string DocId, JsonElement RawDoc)>> FetchHierarchyAsync(
        ICluster sourceCluster, MigrationConfig config)
    {
        var collection = config.HierarchyCollection;
        var query = $@"SELECT META().id AS id, `{collection}` AS doc
                       FROM `{config.SourceBucket}`.`{config.SourceScopeName}`.`{collection}`
                       WHERE `{collection}`.companyId = {config.CompanyId}";

        Logger.Info($"Fetching Hierarchy where companyId = {config.CompanyId}...");

        var results = new List<(string DocId, JsonElement RawDoc)>();
        var queryResult = await sourceCluster.QueryAsync<JObject>(query);

        await foreach (var row in queryResult)
        {
            var docId = row["id"]!.ToString();
            var docJson = row["doc"]!.ToString();
            var jsonElement = JsonDocument.Parse(docJson).RootElement.Clone();
            results.Add((docId, jsonElement));
        }

        Logger.Success($"Fetched {results.Count} Hierarchy documents.");
        return results;
    }

    /// <summary>
    /// Fetch Classification documents by their IDs (batched).
    /// </summary>
    public async Task<List<(string DocId, JsonElement RawDoc)>> FetchClassificationsByIdsAsync(
        ICluster sourceCluster, MigrationConfig config, List<string> ids)
    {
        return await FetchDocumentsByIdsAsync(
            sourceCluster, config.SourceBucket, config.SourceScopeName,
            config.ClassificationCollection, ids, "Classification");
    }

    /// <summary>
    /// Fetch SubClassification documents by their IDs (batched).
    /// </summary>
    public async Task<List<(string DocId, JsonElement RawDoc)>> FetchSubClassificationsByIdsAsync(
        ICluster sourceCluster, MigrationConfig config, List<string> ids)
    {
        return await FetchDocumentsByIdsAsync(
            sourceCluster, config.SourceBucket, config.SourceScopeName,
            config.SubClassificationCollection, ids, "SubClassification");
    }

    private async Task<List<(string DocId, JsonElement RawDoc)>> FetchDocumentsByIdsAsync(
        ICluster sourceCluster, string bucketName, string scopeName,
        string collectionName, List<string> ids, string label)
    {
        var results = new List<(string DocId, JsonElement RawDoc)>();

        if (ids.Count == 0)
        {
            Logger.Info($"No {label} IDs to fetch.");
            return results;
        }

        Logger.Info($"Fetching {ids.Count} {label} documents in batches of {IdBatchSize}...");

        for (int i = 0; i < ids.Count; i += IdBatchSize)
        {
            var batch = ids.Skip(i).Take(IdBatchSize).ToList();
            var idList = string.Join(", ", batch.Select(id => $"\"{id}\""));

            var query = $@"SELECT META().id AS id, `{collectionName}` AS doc
                           FROM `{bucketName}`.`{scopeName}`.`{collectionName}`
                           WHERE META().id IN [{idList}]";

            try
            {
                var queryResult = await sourceCluster.QueryAsync<JObject>(query);
                await foreach (var row in queryResult)
                {
                    var docId = row["id"]!.ToString();
                    var docJson = row["doc"]!.ToString();
                    var jsonElement = JsonDocument.Parse(docJson).RootElement.Clone();
                    results.Add((docId, jsonElement));
                }

                Logger.Info($"  Batch {i / IdBatchSize + 1}: fetched {batch.Count} IDs");
            }
            catch (Exception ex)
            {
                Logger.Error($"Batch {i / IdBatchSize + 1} failed for {label}: {ex.Message}");
            }
        }

        Logger.Success($"Fetched {results.Count} of {ids.Count} {label} documents.");
        return results;
    }
}
