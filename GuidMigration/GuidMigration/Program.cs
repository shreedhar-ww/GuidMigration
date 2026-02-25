using Microsoft.Extensions.Configuration;
using GuidMigration.Models;
using GuidMigration.Services;

Console.WriteLine("=== Couchbase GUID Migration Tool ===\n");

try
{
    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false)
        .Build();

    var sourceSection = configuration.GetSection("Couchbase:Source");
    var targetSection = configuration.GetSection("Couchbase:Target");

    var config = new MigrationConfig
    {
        SourceConnectionString = sourceSection["ConnectionString"]!,
        SourceUsername = sourceSection["Username"]!,
        SourcePassword = sourceSection["Password"]!,
        SourceBucket = sourceSection["Bucket"]!,
        TargetConnectionString = targetSection["ConnectionString"]!,
        TargetUsername = targetSection["Username"]!,
        TargetPassword = targetSection["Password"]!,
        TargetBucket = targetSection["Bucket"]!,
        ScopeName = configuration["Couchbase:ScopeName"] ?? "UCG",
        CompanyId = int.Parse(configuration["Couchbase:CompanyId"] ?? "1"),
        BatchSize = int.Parse(configuration["Couchbase:BatchSize"] ?? "50")
    };

    var runner = new MigrationRunner(config);
    await runner.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"\n[FATAL] Migration failed: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}
