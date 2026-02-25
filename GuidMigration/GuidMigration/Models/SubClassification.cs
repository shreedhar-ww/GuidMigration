using System.Text.Json.Serialization;

namespace GuidMigration.Models;

public class SubClassification
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parent")]
    public string? Parent { get; set; }

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("yield")]
    public double? Yield { get; set; }

    [JsonPropertyName("leaflevel")]
    public int? Leaflevel { get; set; }

    [JsonPropertyName("wet")]
    public double? Wet { get; set; }

    [JsonPropertyName("days")]
    public int? Days { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("microlearning")]
    public object[]? Microlearning { get; set; }

    [JsonPropertyName("jobbing")]
    public object[]? Jobbing { get; set; }

    [JsonPropertyName("tracebility")]
    public object[]? Tracebility { get; set; }

    [JsonPropertyName("microLearningCount")]
    public int MicroLearningCount { get; set; }

    [JsonPropertyName("jobbingCount")]
    public int JobbingCount { get; set; }

    [JsonPropertyName("traceabilityCount")]
    public int TraceabilityCount { get; set; }

    [JsonPropertyName("isdiaryavailable")]
    public int? Isdiaryavailable { get; set; }

    [JsonPropertyName("subCategoryCount")]
    public int SubCategoryCount { get; set; }

    [JsonPropertyName("tableData")]
    public object[]? TableData { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("createdDate")]
    public DateTime? CreatedDate { get; set; }

    [JsonPropertyName("lastModifiedBy")]
    public string? LastModifiedBy { get; set; }

    [JsonPropertyName("lastModifiedDate")]
    public DateTime? LastModifiedDate { get; set; }
}
