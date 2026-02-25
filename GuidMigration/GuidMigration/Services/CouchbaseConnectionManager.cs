using Couchbase;
using Couchbase.Management.Collections;
using GuidMigration.Models;

namespace GuidMigration.Services;

public class CouchbaseConnectionManager : IAsyncDisposable
{
    public ICluster SourceCluster { get; private set; } = null!;
    public ICluster TargetCluster { get; private set; } = null!;
    public IBucket SourceBucket { get; private set; } = null!;
    public IBucket TargetBucket { get; private set; } = null!;

    public async Task ConnectAsync(MigrationConfig config)
    {
        Logger.Info("Connecting to source cluster...");
        SourceCluster = await Cluster.ConnectAsync(
            config.SourceConnectionString,
            config.SourceUsername,
            config.SourcePassword);
        SourceBucket = await SourceCluster.BucketAsync(config.SourceBucket);
        Logger.Success("Connected to source cluster.");

        Logger.Info("Connecting to target cluster...");
        TargetCluster = await Cluster.ConnectAsync(
            config.TargetConnectionString,
            config.TargetUsername,
            config.TargetPassword);
        TargetBucket = await TargetCluster.BucketAsync(config.TargetBucket);
        Logger.Success("Connected to target cluster.");
    }

    public async Task EnsureTargetStructureAsync(MigrationConfig config)
    {
        var manager = TargetBucket.Collections;
        var manifest = await manager.GetAllScopesAsync();

        // Create scope if not exists
        if (!manifest.Any(s => s.Name == config.ScopeName))
        {
            await manager.CreateScopeAsync(config.ScopeName);
            Logger.Success($"Created scope: {config.ScopeName}");
            await Task.Delay(2000);
        }
        else
        {
            Logger.Info($"Scope '{config.ScopeName}' already exists.");
        }

        // Refresh manifest after scope creation
        manifest = await manager.GetAllScopesAsync();
        var scope = manifest.SingleOrDefault(s => s.Name == config.ScopeName);
        if (scope == null)
        {
            throw new Exception($"Scope '{config.ScopeName}' does not exist after creation attempt.");
        }

        var existingCollections = scope.Collections.Select(c => c.Name).ToHashSet();
        var requiredCollections = new[]
        {
            config.ClassificationCollection,
            config.SubClassificationCollection,
            config.HierarchyCollection
        };

        foreach (var collection in requiredCollections)
        {
            if (!existingCollections.Contains(collection))
            {
                await manager.CreateCollectionAsync(config.ScopeName, collection, new CreateCollectionSettings());
                Logger.Success($"Created collection: {collection}");
                await Task.Delay(1000);
            }
            else
            {
                Logger.Info($"Collection '{collection}' already exists.");
            }
        }

        // Create primary indexes
        foreach (var collection in requiredCollections)
        {
            try
            {
                var indexQuery = $"CREATE PRIMARY INDEX IF NOT EXISTS ON `{config.TargetBucket}`.`{config.ScopeName}`.`{collection}`";
                await TargetCluster.QueryAsync<dynamic>(indexQuery);
                Logger.Success($"Primary index ensured for {collection}");
            }
            catch
            {
                Logger.Info($"Primary index already exists for {collection}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (SourceCluster != null) await SourceCluster.DisposeAsync();
        if (TargetCluster != null) await TargetCluster.DisposeAsync();
    }
}
