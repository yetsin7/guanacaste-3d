using System.Text.Json;
using System.Text.Json.Serialization;
using Guanacaste3D.Core;

if (args.Length != 1)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new { error = "ProjectPathRequired" }));
    return 2;
}

try
{
    var document = ProjectFile.Load(args[0]);
    var issues = ModelAudit.Analyze(document);
    var report = new
    {
        projectId = document.Id,
        projectName = document.Name,
        issueCount = issues.Count,
        issues
    };
    var options = new JsonSerializerOptions { WriteIndented = true };
    options.Converters.Add(new JsonStringEnumConverter());
    Console.WriteLine(JsonSerializer.Serialize(report, options));
    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new { error = "ProjectAuditFailed", reason = exception.Message }));
    return 1;
}
