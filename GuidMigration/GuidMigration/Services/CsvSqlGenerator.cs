using Microsoft.VisualBasic.FileIO;

namespace GuidMigration.Services;

public class CsvSqlGenerator
{
    private readonly Dictionary<string, string> _guidMapping;
    private const string ZeroGuid = "00000000-0000-0000-0000-000000000000";

    private static readonly HashSet<string> NumericColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "CropId", "IsActive", "Yield", "Ratio", "CreatedBy", "UpdatedBy",
        "Level", "IsDiarySubmited", "IsAnimalCategory", "IsLiveStock", "CompanyId"
    };

    private static readonly HashSet<string> GuidColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "CropGUID", "ParentCropGUID"
    };

    private static readonly HashSet<string> ExcludedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "CropId"
    };

    public CsvSqlGenerator(IReadOnlyDictionary<string, string> guidMapping)
    {
        _guidMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in guidMapping)
        {
            _guidMapping[kvp.Key.ToUpperInvariant()] = kvp.Value.ToUpperInvariant();
        }
    }

    public void GenerateSqlFromCsv(string csvPath, string outputPath, string tableName = "CropData")
    {
        if (!File.Exists(csvPath))
        {
            Logger.Warn($"CSV file not found: {csvPath}. Skipping SQL generation.");
            return;
        }

        Logger.Info($"Reading CSV: {csvPath}");

        var (headers, rows) = ParseCsv(csvPath);
        var includedIndices = new List<int>();
        var includedHeaders = new List<string>();
        for (int i = 0; i < headers.Length; i++)
        {
            if (!ExcludedColumns.Contains(headers[i]))
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

                if (GuidColumns.Contains(col))
                {
                    values.Add(RemapGuid(raw, ref mappedCount));
                }
                else if (NumericColumns.Contains(col))
                {
                    values.Add(SqlVal(raw, isString: false));
                }
                else
                {
                    values.Add(SqlVal(raw, isString: true));
                }
            }

            var valueList = string.Join(", ", values);
            inserts.Add($"INSERT INTO {tableName} ({columnList}) VALUES ({valueList});");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var writer = new StreamWriter(outputPath);
        writer.WriteLine("-- SQL Insert Script with remapped CropGUID and ParentCropGUID");
        writer.WriteLine($"-- Source CSV: {Path.GetFileName(csvPath)}");
        writer.WriteLine($"-- Total rows: {inserts.Count}");
        writer.WriteLine($"-- GUIDs remapped: {mappedCount}");
        writer.WriteLine();
        foreach (var stmt in inserts)
        {
            writer.WriteLine(stmt);
        }

        Logger.Success($"SQL script generated: {outputPath} ({inserts.Count} rows, {mappedCount} GUIDs remapped)");
    }

    private (string[] Headers, List<string[]> Rows) ParseCsv(string csvPath)
    {
        var rows = new List<string[]>();
        string[]? headers = null;

        using var parser = new TextFieldParser(csvPath);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;

        if (!parser.EndOfData)
        {
            headers = parser.ReadFields()!;
        }

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields != null)
                rows.Add(fields);
        }

        return (headers ?? Array.Empty<string>(), rows);
    }

    private string RemapGuid(string raw, ref int mappedCount)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim().Equals("NULL", StringComparison.OrdinalIgnoreCase))
            return "NULL";

        var guid = raw.Trim().ToUpperInvariant();

        if (guid == ZeroGuid)
            return $"'{ZeroGuid}'";

        if (_guidMapping.TryGetValue(guid, out var newGuid))
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
}
