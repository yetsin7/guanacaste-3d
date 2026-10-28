using System.Text.Json;
using System.Text.Json.Serialization;
using Guanacaste3D.Core;

// The default remains advisory; CI can opt in to failing on actionable findings.
var failOnIssues = args.Length == 2 && args[0] == "--fail-on-issues";
if (!(args.Length == 1 || failOnIssues))
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new { error = "ProjectPathRequired" }));
    return 2;
}

try
{
    var document = ProjectFile.Load(args[failOnIssues ? 1 : 0]);
    var issues = ModelAudit.Analyze(document);
    var findings = ModelAuditReporting.Describe(issues);
    var report = new
    {
        projectId = document.Id,
        projectName = document.Name,
        issueCount = issues.Count,
        issues,
        findings,
        levelSummaries = ModelAuditReporting.SummarizeByLevel(findings)
    };
    var options = new JsonSerializerOptions { WriteIndented = true };
    options.Converters.Add(new JsonStringEnumConverter());
    Console.WriteLine(JsonSerializer.Serialize(report, options));
    return failOnIssues && issues.Count > 0 ? 3 : 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new { error = "ProjectAuditFailed", reason = exception.Message }));
    return 1;
}
