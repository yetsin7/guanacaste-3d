using Guanacaste3D.Core;

var session = new DocumentSession(BuildingDocument.Create("Audit verification"));
var level = session.Document.Levels[0].Id;
var otherLevel = session.AddLevel();
var firstWall = session.AddWall(level, new(0, 0), new(5, 0));
var secondWall = session.AddWall(level, new(5.06, 0), new(10, 0));
var joinedWall = session.AddWall(level, new(2, 0.05), new(2, 2));
_ = session.AddWall(otherLevel, new(5.06, 0), new(10, 0));
var firstRoom = session.AddRoom(level, new(0, 2), new(3, 5));
var secondRoom = session.AddRoom(level, new(2, 3), new(4, 6));
var opening = session.AddOpening(firstWall, OpeningKind.Door, offset: 2, height: 2.95);
var snapshot = session.Document;
var issues = ModelAudit.Analyze(snapshot);

Check(issues.Any(issue => issue.Code == ModelAuditCode.NearWallGap &&
    issue.ElementId == firstWall && issue.RelatedId == secondWall &&
    Math.Abs(issue.ValueMeters - 0.06) < 1e-9), "A 60 mm wall near miss is measured exactly");
Check(issues.Any(issue => issue.Code == ModelAuditCode.OverlappingRooms &&
    issue.ElementId == firstRoom && issue.RelatedId == secondRoom &&
    Math.Abs(issue.ValueMeters - 2) < 1e-9), "Overlapping rooms report square meters");
Check(issues.Any(issue => issue.Code == ModelAuditCode.LowOpeningHeadClearance &&
    issue.ElementId == opening && Math.Abs(issue.ValueMeters - 0.05) < 1e-9),
    "An opening close to the wall top reports its remaining material");
Check(issues.All(issue => issue.LevelId == level) && ReferenceEquals(snapshot, session.Document),
    "Audit ignores unrelated levels and does not mutate the editing session");
Check(issues.All(issue => issue.ElementId != joinedWall),
    "A wall already meeting another wall body is not reported as a gap");
Check(ModelAudit.Analyze(ProjectFile.DeserializeDocument(ProjectFile.SerializeDocument(snapshot)))
    .SequenceEqual(issues), "Audit findings are stable after native save and reopen");

var findings = ModelAuditReporting.Describe(issues);
Check(findings.Count == issues.Count && findings.All(finding =>
    !string.IsNullOrWhiteSpace(finding.Description) &&
    !string.IsNullOrWhiteSpace(finding.SuggestedAction)),
    "Every audit issue has actionable repair guidance");
Check(findings.Any(finding => finding.Issue.Code == ModelAuditCode.NearWallGap &&
    finding.Severity == ModelAuditSeverity.Warning),
    "Near wall gaps have warning severity");

session.MoveWall(secondWall, 0.5, 0);
Check(!ModelAudit.Analyze(session.Document).Any(issue => issue.Code == ModelAuditCode.NearWallGap),
    "Fixing the wall gap removes the finding");

Console.WriteLine("Model audit checks passed.");

/// <summary>Fails with the behavior under review when an audit condition is unmet.</summary>
static void Check(bool valid, string description)
{
    if (!valid) throw new InvalidOperationException(description);
    Console.WriteLine($"OK: {description}");
}
