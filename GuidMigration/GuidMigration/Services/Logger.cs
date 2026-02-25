namespace GuidMigration.Services;

public static class Logger
{
    private static readonly string LogDirectory;
    private static readonly string LogFilePath;
    private static readonly object Lock = new();

    static Logger()
    {
        LogDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "logs");
        Directory.CreateDirectory(LogDirectory);
        LogFilePath = Path.Combine(LogDirectory, $"migration-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
    }

    public static string GetLogFilePath() => LogFilePath;
    public static string GetLogDirectory() => LogDirectory;

    public static void Info(string message) => Log("INFO", message, ConsoleColor.Cyan);
    public static void Success(string message) => Log("OK", message, ConsoleColor.Green);
    public static void Warn(string message) => Log("WARN", message, ConsoleColor.Yellow);
    public static void Error(string message) => Log("ERROR", message, ConsoleColor.Red);
    public static void Section(string message) => Log("====", message, ConsoleColor.Magenta);

    private static void Log(string level, string message, ConsoleColor color)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logLine = $"[{timestamp}] [{level}] {message}";

        // Console output with color
        var prevColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(logLine);
        Console.ForegroundColor = prevColor;

        // File output
        lock (Lock)
        {
            File.AppendAllText(LogFilePath, logLine + Environment.NewLine);
        }
    }

    /// <summary>
    /// Write the GUID mapping and summary to a JSON report file for analysis.
    /// </summary>
    public static void WriteAnalysisReport(
        string reportDir,
        IReadOnlyDictionary<string, string> guidMappings,
        int classificationCount, int subClassificationCount, int hierarchyCount,
        int cSuccess, int cFailed,
        int sSuccess, int sFailed,
        int hSuccess, int hFailed)
    {
        Directory.CreateDirectory(reportDir);

        // Write GUID mapping CSV
        var mappingFile = Path.Combine(reportDir, "guid-mapping.csv");
        var csvLines = new List<string> { "OldGuid,NewGuid" };
        foreach (var kvp in guidMappings)
        {
            csvLines.Add($"{kvp.Key},{kvp.Value}");
        }
        File.WriteAllLines(mappingFile, csvLines);
        Info($"GUID mapping written to: {mappingFile}");

        // Write summary report
        var summaryFile = Path.Combine(reportDir, "migration-summary.txt");
        var lines = new List<string>
        {
            $"Migration Report - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            new string('=', 50),
            "",
            "Source Fetch Counts:",
            $"  Hierarchy (companyId filter):   {hierarchyCount}",
            $"  Classification (level=1):       {classificationCount}",
            $"  SubClassification (level>1):    {subClassificationCount}",
            "",
            "Target Upsert Results:",
            $"  Classification:    {cSuccess} success, {cFailed} failed",
            $"  SubClassification: {sSuccess} success, {sFailed} failed",
            $"  Hierarchy:         {hSuccess} success, {hFailed} failed",
            "",
            $"Total GUID mappings created: {guidMappings.Count}",
            "",
            "GUID Mappings (OldId -> NewId):",
            new string('-', 80)
        };

        foreach (var kvp in guidMappings)
        {
            lines.Add($"  {kvp.Key}  ->  {kvp.Value}");
        }

        File.WriteAllLines(summaryFile, lines);
        Info($"Migration summary written to: {summaryFile}");
    }
}
