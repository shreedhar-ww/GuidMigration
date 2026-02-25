using System.Diagnostics;
using System.Text.Json.Nodes;
using GuidMigration.Models;
using Microsoft.VisualBasic.FileIO;

namespace GuidMigration.Services;

public class MigrationRunner
{
    private readonly MigrationConfig _config;

    public MigrationRunner(MigrationConfig config)
    {
        _config = config;
    }

    public async Task RunAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        Logger.Section("=========================================");
        Logger.Section(" GUID MIGRATION STARTED");
        Logger.Section("=========================================");
        Logger.Info($"Source: {_config.SourceConnectionString} / {_config.SourceBucket}");
        Logger.Info($"Target: {_config.TargetConnectionString} / {_config.TargetBucket}");
        Logger.Info($"Source Scope: {_config.SourceScopeName}");
        Logger.Info($"Target Scope: {_config.TargetScopeName}");
        Logger.Info($"CompanyId: {_config.CompanyId}");
        Logger.Info($"Log file: {Logger.GetLogFilePath()}");
        Logger.Section("=========================================");

        // --- Step 1: Connect ---
        await using var connManager = new CouchbaseConnectionManager();
        await connManager.ConnectAsync(_config);

        // --- Step 2: Ensure target scope + collections + indexes ---
        await connManager.EnsureTargetStructureAsync(_config);

        // --- Step 3: Fetch Hierarchy ---
        Logger.Section("--- STEP 3: Fetch Hierarchy ---");
        var fetcher = new DataFetcher();
        var hierarchyDocs = await fetcher.FetchHierarchyAsync(connManager.SourceCluster, _config);

        if (hierarchyDocs.Count == 0)
        {
            Logger.Warn("No Hierarchy documents found. Nothing to migrate.");
            return;
        }

        // --- Step 4: Separate by level ---
        Logger.Section("--- STEP 4: Separate Hierarchy by level ---");
        var classificationIds = new List<string>();
        var subClassificationIds = new List<string>();

        foreach (var (docId, rawDoc) in hierarchyDocs)
        {
            var level = rawDoc.GetProperty("level").GetInt32();
            if (level == 1)
                classificationIds.Add(docId);
            else
                subClassificationIds.Add(docId);
        }

        classificationIds = classificationIds.Distinct().ToList();
        subClassificationIds = subClassificationIds.Distinct().ToList();

        Logger.Info($"Classification IDs (level=1): {classificationIds.Count}");
        Logger.Info($"SubClassification IDs (level>1): {subClassificationIds.Count}");

        // --- Step 5: Fetch Classification docs ---
        Logger.Section("--- STEP 5: Fetch Classifications ---");
        var classificationDocs = await fetcher.FetchClassificationsByIdsAsync(
            connManager.SourceCluster, _config, classificationIds);

        // --- Step 6: Fetch SubClassification docs ---
        Logger.Section("--- STEP 6: Fetch SubClassifications ---");
        var subClassificationDocs = await fetcher.FetchSubClassificationsByIdsAsync(
            connManager.SourceCluster, _config, subClassificationIds);

        // --- Step 7: Register ALL IDs in GuidRemapper ---
        Logger.Section("--- STEP 7: Register GUID mappings ---");
        var remapper = new GuidRemapper();
        var transformer = new DocumentTransformer(remapper);

        var allOldIds = classificationDocs.Select(d => d.DocId)
            .Concat(subClassificationDocs.Select(d => d.DocId))
            .Distinct()
            .ToList();

        transformer.RegisterAllIds(allOldIds);

        // --- Step 8: Transform Classifications ---
        Logger.Section("--- STEP 8: Transform Classifications ---");
        var transformedClassifications = new List<(string Key, JsonObject Document)>();
        foreach (var (docId, rawDoc) in classificationDocs)
        {
            var result = transformer.TransformClassification(docId, rawDoc);
            if (result.HasValue)
                transformedClassifications.Add(result.Value);
        }

        // --- Step 9: Transform SubClassifications ---
        Logger.Section("--- STEP 9: Transform SubClassifications ---");
        var transformedSubClassifications = new List<(string Key, JsonObject Document)>();
        foreach (var (docId, rawDoc) in subClassificationDocs)
        {
            var result = transformer.TransformSubClassification(docId, rawDoc);
            if (result.HasValue)
                transformedSubClassifications.Add(result.Value);
        }

        // --- Step 10: Transform Hierarchies ---
        Logger.Section("--- STEP 10: Transform Hierarchies ---");
        var transformedHierarchies = new List<(string Key, JsonObject Document)>();
        foreach (var (docId, rawDoc) in hierarchyDocs)
        {
            var result = transformer.TransformHierarchy(docId, rawDoc);
            if (result.HasValue)
                transformedHierarchies.Add(result.Value);
        }

        // --- Step 11: Upsert to target + Generate SQL INSERT script ---
        var upserter = new DocumentUpserter(_config.BatchSize);
        var targetScope = connManager.TargetBucket.Scope(_config.TargetScopeName);
        var reportDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "reports",
            $"migration-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
        var mappings = remapper.GetAllMappings();

        Logger.Section("--- STEP 11a: Upsert Classifications ---");
        var classTarget = await targetScope.CollectionAsync(_config.ClassificationCollection);
        var (cSuccess, cFailed, cFailedIds) = await upserter.UpsertAsync(classTarget, transformedClassifications);

        Logger.Section("--- STEP 11b: Upsert SubClassifications ---");
        var subClassTarget = await targetScope.CollectionAsync(_config.SubClassificationCollection);
        var (sSuccess, sFailed, sFailedIds) = await upserter.UpsertAsync(subClassTarget, transformedSubClassifications);

        Logger.Section("--- STEP 11c: Upsert Hierarchies ---");
        var hierTarget = await targetScope.CollectionAsync(_config.HierarchyCollection);
        var (hSuccess, hFailed, hFailedIds) = await upserter.UpsertAsync(hierTarget, transformedHierarchies);

        // --- Step 11d: Generate SQL INSERT script from CSV using GUID mappings ---
        if (!string.IsNullOrWhiteSpace(_config.CsvFilePath))
        {
            Logger.Section("--- STEP 11d: Generate SQL INSERT Script ---");
            var csvPath = Path.IsPathRooted(_config.CsvFilePath)
                ? _config.CsvFilePath
                : Path.Combine(AppContext.BaseDirectory, _config.CsvFilePath);
            var sqlOutputPath = Path.Combine(reportDir, $"{_config.SqlTableName}_insert.sql");
            GenerateSqlInsertScript(csvPath, sqlOutputPath, _config.SqlTableName, mappings);
        }

        // --- Step 12: Verification ---
        Logger.Section("--- STEP 12: Verification ---");
        await VerifyMigrationAsync(connManager.TargetCluster, _config,
            transformedClassifications.Count, transformedSubClassifications.Count, transformedHierarchies.Count);

        // --- Write analysis report files ---
        Logger.WriteAnalysisReport(
            reportDir,
            mappings,
            classificationDocs.Count, subClassificationDocs.Count, hierarchyDocs.Count,
            cSuccess, cFailed,
            sSuccess, sFailed,
            hSuccess, hFailed);

        // Log GUID mappings to console/log file
        Logger.Section("--- GUID Mapping Summary ---");
        int count = 0;
        foreach (var kvp in mappings)
        {
            Logger.Info($"  {kvp.Key} -> {kvp.Value}");
            count++;
            if (count >= 30)
            {
                Logger.Info($"  ... and {mappings.Count - 30} more.");
                break;
            }
        }

        // Final summary
        stopwatch.Stop();
        Logger.Section("=========================================");
        Logger.Section(" MIGRATION COMPLETE");
        Logger.Section("=========================================");
        Logger.Success($"Classification:    {cSuccess} success, {cFailed} failed");
        Logger.Success($"SubClassification: {sSuccess} success, {sFailed} failed");
        Logger.Success($"Hierarchy:         {hSuccess} success, {hFailed} failed");
        Logger.Success($"Total GUID mappings: {remapper.Count}");
        Logger.Success($"Duration: {stopwatch.Elapsed.TotalSeconds:F1}s");
        Logger.Info($"Log file: {Logger.GetLogFilePath()}");
        Logger.Info($"Report folder: {reportDir}");
        Logger.Section("=========================================");

        if (cFailed > 0 || sFailed > 0 || hFailed > 0)
        {
            Logger.Warn("Some documents failed to upsert:");
            if (cFailedIds.Count > 0)
                Logger.Warn($"  Classification failed IDs: {string.Join(", ", cFailedIds)}");
            if (sFailedIds.Count > 0)
                Logger.Warn($"  SubClassification failed IDs: {string.Join(", ", sFailedIds)}");
            if (hFailedIds.Count > 0)
                Logger.Warn($"  Hierarchy failed IDs: {string.Join(", ", hFailedIds)}");
        }
    }

    /// <summary>
    /// Reads the CSV, replaces CropGUID and ParentCropGUID using the GUID mappings,
    /// and generates SQL INSERT statements.
    /// </summary>
    private static void GenerateSqlInsertScript(
        string csvPath, string outputPath, string tableName,
        IReadOnlyDictionary<string, string> guidMappings)
    {
        if (!File.Exists(csvPath))
        {
            Logger.Warn($"CSV file not found: {csvPath}. Skipping SQL generation.");
            return;
        }

        Logger.Info($"Reading CSV: {csvPath}");

        var guidMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in guidMappings)
            guidMap[kvp.Key.ToUpperInvariant()] = kvp.Value.ToUpperInvariant();

        // Parse CSV
        var rows = new List<string[]>();
        string[] headers;
        using (var parser = new TextFieldParser(csvPath))
        {
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(",");
            parser.HasFieldsEnclosedInQuotes = true;

            headers = parser.EndOfData ? Array.Empty<string>() : parser.ReadFields()!;
            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields != null) rows.Add(fields);
            }
        }

        // Column classification
        var numericColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CropId", "IsActive", "Yield", "Ratio", "CreatedBy", "UpdatedBy",
            "Level", "IsDiarySubmited", "IsAnimalCategory", "IsLiveStock", "CompanyId"
        };
        var guidColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CropGUID", "ParentCropGUID"
        };
        var excludedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CropId"
        };

        // Build included column indices (skip CropId)
        var includedIndices = new List<int>();
        var includedHeaders = new List<string>();
        for (int i = 0; i < headers.Length; i++)
        {
            if (!excludedColumns.Contains(headers[i]))
            {
                includedIndices.Add(i);
                includedHeaders.Add(headers[i]);
            }
        }

        var columnList = string.Join(", ", includedHeaders);
        var inserts = new List<string>();
        int mappedCount = 0;

        foreach (var row in rows)
        {
            var values = new List<string>();
            foreach (var i in includedIndices)
            {
                var col = headers[i];
                var raw = i < row.Length ? row[i] : "";

                if (guidColumns.Contains(col))
                {
                    values.Add(RemapGuid(raw, guidMap, ref mappedCount));
                }
                else if (numericColumns.Contains(col))
                {
                    values.Add(SqlVal(raw, isString: false));
                }
                else
                {
                    values.Add(SqlVal(raw, isString: true));
                }
            }

            inserts.Add($"INSERT INTO {tableName} ({columnList}) VALUES ({string.Join(", ", values)});");
        }

        // Write SQL file
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var writer = new StreamWriter(outputPath);
        writer.WriteLine($"-- SQL Insert Script with remapped CropGUID and ParentCropGUID");
        writer.WriteLine($"-- Source CSV: {Path.GetFileName(csvPath)}");
        writer.WriteLine($"-- Total rows: {inserts.Count}");
        writer.WriteLine($"-- GUIDs remapped: {mappedCount}");
        writer.WriteLine();
        foreach (var stmt in inserts)
            writer.WriteLine(stmt);

        Logger.Success($"SQL script generated: {outputPath} ({inserts.Count} rows, {mappedCount} GUIDs remapped)");
    }

    private static string RemapGuid(string raw, Dictionary<string, string> guidMap, ref int mappedCount)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim().Equals("NULL", StringComparison.OrdinalIgnoreCase))
            return "NULL";

        var guid = raw.Trim().ToUpperInvariant();

        if (guid == "00000000-0000-0000-0000-000000000000")
            return $"'{guid}'";

        if (guidMap.TryGetValue(guid, out var newGuid))
        {
            mappedCount++;
            return $"'{newGuid}'";
        }

        return $"'{guid}'";
    }

    private static string SqlVal(string raw, bool isString)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim().Equals("NULL", StringComparison.OrdinalIgnoreCase))
            return "NULL";

        var val = raw.Trim();
        if (isString)
            return "'" + val.Replace("'", "''") + "'";
        return val;
    }

    private async Task VerifyMigrationAsync(
        Couchbase.ICluster targetCluster, MigrationConfig config,
        int expectedClassifications, int expectedSubClassifications, int expectedHierarchies)
    {
        var collections = new[]
        {
            (config.ClassificationCollection, expectedClassifications),
            (config.SubClassificationCollection, expectedSubClassifications),
            (config.HierarchyCollection, expectedHierarchies)
        };

        foreach (var (collectionName, expected) in collections)
        {
            try
            {
                var query = $"SELECT COUNT(*) AS cnt FROM `{config.TargetBucket}`.`{config.TargetScopeName}`.`{collectionName}`";
                var result = await targetCluster.QueryAsync<Newtonsoft.Json.Linq.JObject>(query);

                await foreach (var row in result)
                {
                    var actual = (int)row["cnt"]!;
                    if (actual >= expected)
                        Logger.Success($"{collectionName}: {actual} docs in target (expected >= {expected})");
                    else
                        Logger.Warn($"{collectionName}: {actual} docs in target (expected >= {expected}) -- MISMATCH");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Verification failed for {collectionName}: {ex.Message}");
            }
        }
    }
}
