using System.Text.Json.Serialization;

namespace GuidMigration.Models;

public class Classification
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("companyId")]
    public int CompanyId { get; set; }

    [JsonPropertyName("classification")]
    public string? ClassificationName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("nodelevel")]
    public int Nodelevel { get; set; }

    [JsonPropertyName("navigationRoute")]
    public string? NavigationRoute { get; set; }

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

    [JsonPropertyName("subCategoryCount")]
    public int SubCategoryCount { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("isAnimalCategory")]
    public bool IsAnimalCategory { get; set; }

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("createdDate")]
    public DateTime? CreatedDate { get; set; }

    [JsonPropertyName("lastModifiedBy")]
    public string? LastModifiedBy { get; set; }

    [JsonPropertyName("lastModifiedDate")]
    public DateTime? LastModifiedDate { get; set; }
}
