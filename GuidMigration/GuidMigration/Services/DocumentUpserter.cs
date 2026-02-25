using System.Text.Json;
using System.Text.Json.Nodes;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;

namespace GuidMigration.Services;

public class DocumentUpserter
{
    private readonly int _batchSize;

    public DocumentUpserter(int batchSize = 50)
    {
        _batchSize = batchSize;
    }

    /// <summary>
    /// Upsert documents to the target collection in parallel batches.
    /// Returns (successCount, failedCount, failedIds).
    /// </summary>
    public async Task<(int Success, int Failed, List<string> FailedIds)> UpsertAsync(
        ICouchbaseCollection targetCollection,
        List<(string Key, JsonObject Document)> documents)
    {
        int success = 0, failed = 0;
        var failedIds = new List<string>();

        for (int i = 0; i < documents.Count; i += _batchSize)
        {
            var batch = documents.Skip(i).Take(_batchSize).ToList();

            var tasks = batch.Select(async doc =>
            {
                try
                {
                    var jsonBytes = System.Text.Encoding.UTF8.GetBytes(doc.Document.ToJsonString());
                    await targetCollection.UpsertAsync(doc.Key, jsonBytes,
                        new UpsertOptions().Transcoder(new RawJsonTranscoder()));
                    return (doc.Key, IsSuccess: true, Error: (string?)null);
                }
                catch (Exception ex)
                {
                    return (doc.Key, IsSuccess: false, Error: ex.Message);
                }
            }).ToList();

            var results = await Task.WhenAll(tasks);

            foreach (var (key, isSuccess, error) in results)
            {
                if (isSuccess)
                {
                    success++;
                }
                else
                {
                    failed++;
                    failedIds.Add(key);
                    Logger.Error($"Upsert failed for {key}: {error}");
                }
            }

            Logger.Info($"  Batch {i / _batchSize + 1}: {batch.Count} docs processed");
        }

        return (success, failed, failedIds);
    }
}
