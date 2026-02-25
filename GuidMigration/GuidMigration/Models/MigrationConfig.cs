namespace GuidMigration.Models;

public class MigrationConfig
{
    // Source
    public string SourceConnectionString { get; set; } = string.Empty;
    public string SourceUsername { get; set; } = string.Empty;
    public string SourcePassword { get; set; } = string.Empty;
    public string SourceBucket { get; set; } = string.Empty;

    // Target
    public string TargetConnectionString { get; set; } = string.Empty;
    public string TargetUsername { get; set; } = string.Empty;
    public string TargetPassword { get; set; } = string.Empty;
    public string TargetBucket { get; set; } = string.Empty;

    // Scope and Collections
    public string SourceScopeName { get; set; } = "UCG";
    public string TargetScopeName { get; set; } = "UCG";
    public string ClassificationCollection { get; set; } = "Classification";
    public string SubClassificationCollection { get; set; } = "SubClassification";
    public string HierarchyCollection { get; set; } = "Hierarchy";

    // Filter
    public int CompanyId { get; set; } = 1;

    // Batching
    public int BatchSize { get; set; } = 50;

    // CSV to SQL generation
    public string CsvFilePath { get; set; } = string.Empty;
    public string SqlTableName { get; set; } = "CropData";

}
