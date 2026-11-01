using System.Text.RegularExpressions;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Interop;

/// <summary>IFC4 export checks: GlobalId encoding, STEP structure, IFC4 attribute arity, and content.</summary>
static class IfcChecks
{
    // Number of explicit attributes per entity in the IFC4 ADD2 TC1 schema.
    private static readonly Dictionary<string, int> Arity = new(StringComparer.Ordinal)
    {
        ["IFCSIUNIT"] = 4, ["IFCUNITASSIGNMENT"] = 1, ["IFCCARTESIANPOINT"] = 1, ["IFCDIRECTION"] = 1,
        ["IFCAXIS2PLACEMENT3D"] = 3, ["IFCAXIS2PLACEMENT2D"] = 2, ["IFCLOCALPLACEMENT"] = 2,
        ["IFCGEOMETRICREPRESENTATIONCONTEXT"] = 6, ["IFCGEOMETRICREPRESENTATIONSUBCONTEXT"] = 10,
        ["IFCPROJECT"] = 9, ["IFCSITE"] = 14, ["IFCBUILDING"] = 12, ["IFCBUILDINGSTOREY"] = 10,
        ["IFCRELAGGREGATES"] = 6, ["IFCRELCONTAINEDINSPATIALSTRUCTURE"] = 6, ["IFCRELASSOCIATESMATERIAL"] = 6,
        ["IFCRELVOIDSELEMENT"] = 6, ["IFCRELFILLSELEMENT"] = 6, ["IFCMATERIAL"] = 3,
        ["IFCWALL"] = 9, ["IFCSLAB"] = 9, ["IFCOPENINGELEMENT"] = 9, ["IFCDOOR"] = 13, ["IFCWINDOW"] = 13,
        ["IFCSPACE"] = 11, ["IFCRECTANGLEPROFILEDEF"] = 5, ["IFCARBITRARYCLOSEDPROFILEDEF"] = 3,
        ["IFCPOLYLINE"] = 1, ["IFCEXTRUDEDAREASOLID"] = 4, ["IFCSHAPEREPRESENTATION"] = 4,
        ["IFCPRODUCTDEFINITIONSHAPE"] = 3
    };

    public static void Run(Action<bool, string> check)
    {
        check(IfcExporter.ToGlobalId(Guid.Empty) == "0000000000000000000000" &&
              IfcExporter.ToGlobalId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")) == "3$$$$$$$$$$$$$$$$$$$$$",
            "IFC GlobalId encodes the 128-bit extremes");
        var sampleGuid = Guid.Parse("0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0");
        check(DecodeGlobalId(IfcExporter.ToGlobalId(sampleGuid)) == sampleGuid,
            "IFC GlobalId round-trips through the base-64 decoder");
        check(IfcExporter.Real(2) == "2." && IfcExporter.Real(-0.2) == "-0.2" && IfcExporter.Real(0) == "0.",
            "IFC reals always carry a decimal point");
        var preciseValues = new[] { 1e-13, -1e-13, 0.00000000000042, 1.2345678901234567, 1234567890123.125 };
        check(preciseValues.All(value =>
                double.Parse(IfcExporter.Real(value), System.Globalization.CultureInfo.InvariantCulture) == value) &&
              IfcExporter.Real(1e-13).Contains(".E-", StringComparison.Ordinal),
            "IFC STEP reals preserve tiny coordinates and full double precision through scientific notation");
        check(IfcExporter.Text("Sala 'A' ñ\\") == @"'Sala ''A'' \X2\00F1\X0\\\'",
            "IFC strings escape quotes, backslashes and non-ASCII");

        var house = SampleProjects.CreateDemonstrationHouse();
        var stamp = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        var step = IfcExporter.ToStep(house, stamp);
        check(step.StartsWith("ISO-10303-21;\nHEADER;", StringComparison.Ordinal) &&
              step.Contains("FILE_SCHEMA(('IFC4'));", StringComparison.Ordinal) &&
              step.EndsWith("END-ISO-10303-21;\n", StringComparison.Ordinal),
            "IFC export has a STEP header, IFC4 schema and terminator");
        check(IfcExporter.ToStep(house, stamp) == step, "IFC export is deterministic");

        var entities = new Dictionary<int, (string Type, List<string> Attributes)>();
        var data = step[(step.IndexOf("DATA;\n", StringComparison.Ordinal) + 6)..step.IndexOf("ENDSEC;\nEND-ISO", StringComparison.Ordinal)];
        var malformed = 0;
        foreach (var line in data.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(line, @"^#(\d+)=([A-Z0-9]+)\((.*)\);$");
            if (!match.Success) { malformed++; continue; }
            entities[int.Parse(match.Groups[1].Value)] = (match.Groups[2].Value, SplitTopLevel(match.Groups[3].Value));
        }
        check(malformed == 0 && entities.Keys.Order().SequenceEqual(Enumerable.Range(1, entities.Count)),
            "IFC DATA lines are well-formed and sequentially numbered");
        var dangling = Regex.Matches(data, @"#(\d+)").Select(m => int.Parse(m.Groups[1].Value))
            .Count(reference => !entities.ContainsKey(reference));
        check(dangling == 0, "Every IFC entity reference resolves");
        var wrongArity = entities.Values.Where(e => !Arity.TryGetValue(e.Type, out var n) || n != e.Attributes.Count)
            .Select(e => $"{e.Type}/{e.Attributes.Count}").Distinct().ToArray();
        check(wrongArity.Length == 0, "IFC entities match IFC4 attribute counts" +
            (wrongArity.Length == 0 ? "" : ": " + string.Join(", ", wrongArity)));

        int Count(string type) => entities.Values.Count(e => e.Type == type);
        check(Count("IFCWALL") == house.Walls.Count && Count("IFCSLAB") == house.Floors.Count &&
              Count("IFCOPENINGELEMENT") == house.Openings.Count &&
              Count("IFCDOOR") + Count("IFCWINDOW") == house.Openings.Count &&
              Count("IFCRELVOIDSELEMENT") == house.Openings.Count && Count("IFCRELFILLSELEMENT") == house.Openings.Count &&
              Count("IFCSPACE") == house.Rooms.Count && Count("IFCBUILDINGSTOREY") == house.Levels.Count,
            "IFC export contains every wall, slab, opening, fill, space and storey");
        var globalIds = entities.Values.Where(e => e.Type is not ("IFCSIUNIT" or "IFCUNITASSIGNMENT") &&
                e.Attributes.Count > 0 && Regex.IsMatch(e.Attributes[0], @"^'[0-9A-Za-z_$]{22}'$"))
            .Select(e => e.Attributes[0]).ToArray();
        check(globalIds.Length > house.Walls.Count && globalIds.Distinct().Count() == globalIds.Length,
            "IFC GlobalIds are unique");
        check(step.Contains("IFCSPACE(", StringComparison.Ordinal) &&
              step.Contains(@"'Sala-comedor-cocina'", StringComparison.Ordinal),
            "IFC spaces carry room numbers and names");
    }

    /// <summary>Splits STEP attributes on commas outside nested parentheses and strings.</summary>
    private static List<string> SplitTopLevel(string text)
    {
        var parts = new List<string>();
        var depth = 0;
        var inString = false;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\'') inString = !inString; // '' escapes toggle twice and cancel out
            else if (inString) continue;
            else if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0)
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }
        parts.Add(text[start..]);
        return parts;
    }

    /// <summary>Independent decoder used only to verify the encoder.</summary>
    private static Guid DecodeGlobalId(string globalId)
    {
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";
        var value = System.Numerics.BigInteger.Zero;
        foreach (var c in globalId) value = value * 64 + alphabet.IndexOf(c);
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        var padded = new byte[16];
        bytes.CopyTo(padded, 16 - bytes.Length);
        return new Guid(padded, bigEndian: true);
    }
}
