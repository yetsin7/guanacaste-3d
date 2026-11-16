//! End-to-end fixture produced by the actual C# ProjectFile/NativeDocumentStore.
//! Set GUANACASTE_CSHARP_PARITY=1 to require dotnet 10 and run this contract.
use std::{
    fs,
    path::PathBuf,
    process::Command,
    time::{SystemTime, UNIX_EPOCH},
};

#[test]
fn csharp_serializers_match_rust_wall_metrics() {
    if std::env::var_os("GUANACASTE_CSHARP_PARITY").is_none() {
        eprintln!("C# parity not requested; set GUANACASTE_CSHARP_PARITY=1");
        return;
    }
    let nonce = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_nanos();
    let directory =
        std::env::temp_dir().join(format!("g3d-csharp-parity-{}-{nonce}", std::process::id()));
    fs::create_dir(&directory).unwrap();
    let project =
        PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../../interop-fixtures/WallFixture.csproj");
    let generated = Command::new("dotnet")
        .args(["run", "--project"])
        .arg(&project)
        .args(["--configuration", "Release", "--"])
        .arg(&directory)
        .output()
        .expect("dotnet 10 SDK must be installed for C# parity");
    assert!(
        generated.status.success(),
        "C# fixture generation failed: {}",
        String::from_utf8_lossy(&generated.stderr)
    );
    // Compare the actual C# fixture oracle, not only a constant duplicated in Rust.
    let csharp_report = String::from_utf8(generated.stdout).unwrap();
    let csharp_report = csharp_report.trim().replace("\r\n", "\n");
    assert_eq!(
        csharp_report,
        concat!(
            "00000000000000000000000000000002, 2.400000000000, 26.800000000000\n",
            "00000000000000000000000000000003, 3.000000000000, 33.200000000000\n",
            "00000000000000000000000000000004, 3.000000000000, 33.200000000000\n",
            "00000000000000000000000000000005, 1.500000000000, 12.700000000000\n",
            "00000000000000000000000000000006, 3.000000000000, 33.200000000000"
        ),
        "C# fixture dimensions or GUID changed unexpectedly"
    );

    let executable = env!("CARGO_BIN_EXE_wall-metrics");
    let mut reports = Vec::new();
    for filename in ["wall.g3d.json", "wall.g3d"] {
        let path = directory.join(filename);
        let before = fs::read(&path).unwrap();
        let output = Command::new(executable).arg(&path).output().unwrap();
        assert!(
            output.status.success(),
            "{filename}: {}",
            String::from_utf8_lossy(&output.stderr)
        );
        assert_eq!(
            fs::read(&path).unwrap(),
            before,
            "Rust must not mutate C# output"
        );
        reports.push(
            String::from_utf8(output.stdout)
                .unwrap()
                .replace("\r\n", "\n"),
        );
    }
    assert_eq!(
        reports[0], reports[1],
        "C# native and JSON outputs differ in Rust"
    );
    assert_eq!(
        reports[0].trim(),
        csharp_report,
        "Rust GUID, volume or area differ from the C# fixture oracle"
    );
    // A real C# native envelope must reject payload tampering. Mutate a copy only;
    // the original fixture remains byte-for-byte unchanged.
    let native_path = directory.join("wall.g3d");
    let native_bytes = fs::read(&native_path).unwrap();
    let mut corrupted = native_bytes.clone();
    let last = corrupted.last_mut().expect("nonempty C# native fixture");
    *last ^= 0x01;
    let corrupt_path = directory.join("corrupt-wall.g3d");
    fs::write(&corrupt_path, corrupted).unwrap();
    let rejected = Command::new(executable)
        .arg(&corrupt_path)
        .output()
        .unwrap();
    assert!(
        !rejected.status.success(),
        "Rust must reject a corrupted C# native fixture"
    );
    assert!(
        rejected.stdout.is_empty(),
        "failed inspection must not emit partial wall metrics"
    );
    assert_eq!(fs::read(&native_path).unwrap(), native_bytes);

    // A truncated copy of a genuine C# container must fail without partial output.
    let truncated_path = directory.join("truncated-wall.g3d");
    fs::write(&truncated_path, &native_bytes[..native_bytes.len() / 2]).unwrap();
    let truncated = Command::new(executable)
        .arg(&truncated_path)
        .output()
        .unwrap();
    assert!(
        !truncated.status.success(),
        "Rust must reject a truncated C# native fixture"
    );
    assert!(truncated.stdout.is_empty());
    assert_eq!(fs::read(&native_path).unwrap(), native_bytes);

    // Truncation of a genuine C# JSON document must also fail atomically.
    // Work exclusively on a copy to protect the reference fixture.
    let json_path = directory.join("wall.g3d.json");
    let json_bytes = fs::read(&json_path).unwrap();
    let truncated_json_path = directory.join("truncated-wall.g3d.json");
    fs::write(&truncated_json_path, &json_bytes[..json_bytes.len() / 2]).unwrap();
    let rejected_json = Command::new(executable)
        .arg(&truncated_json_path)
        .output()
        .unwrap();
    assert!(
        !rejected_json.status.success(),
        "Rust must reject truncated C# serialized JSON"
    );
    assert!(
        rejected_json.stdout.is_empty(),
        "failed JSON inspection must not emit partial metrics"
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // A syntactically valid but geometrically altered copy must not silently
    // reuse the original C# oracle. The production fixture stays unchanged.
    let mut changed: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    let original_height = changed["Walls"][0]["Height"].as_f64().unwrap();
    changed["Walls"][0]["Height"] = serde_json::json!(original_height + 1.0);
    let changed_path = directory.join("modified-wall.g3d.json");
    fs::write(&changed_path, serde_json::to_vec(&changed).unwrap()).unwrap();
    let changed_result = Command::new(executable)
        .arg(&changed_path)
        .output()
        .unwrap();
    assert!(
        changed_result.status.success(),
        "valid edited JSON should be inspected: {}",
        String::from_utf8_lossy(&changed_result.stderr)
    );
    let changed_report = String::from_utf8(changed_result.stdout).unwrap();
    let mut expected_lines: Vec<String> = csharp_report.lines().map(str::to_owned).collect();
    assert_eq!(expected_lines.len(), 5, "expected five C# fixture walls");
    expected_lines[0] =
        "00000000000000000000000000000002, 3.200000000000, 35.200000000000".to_owned();
    assert_eq!(
        changed_report.trim().replace("\r\n", "\n"),
        expected_lines.join("\n"),
        "Rust must recompute the edited wall and leave all other wall metrics unchanged"
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // Translating a real C# wall to survey coordinates must preserve its
    // volume and surface area. Only the copied JSON is edited.
    let mut translated: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    translated["Walls"][0]["Start"]["X"] = serde_json::json!(1_000_000.0);
    translated["Walls"][0]["Start"]["Y"] = serde_json::json!(-1_000_000.0);
    translated["Walls"][0]["End"]["X"] = serde_json::json!(1_000_004.0);
    translated["Walls"][0]["End"]["Y"] = serde_json::json!(-1_000_000.0);
    let translated_path = directory.join("translated-wall.g3d.json");
    fs::write(&translated_path, serde_json::to_vec(&translated).unwrap()).unwrap();
    let translated_result = Command::new(executable)
        .arg(&translated_path)
        .output()
        .unwrap();
    assert!(
        translated_result.status.success(),
        "translated C# wall should remain valid: {}",
        String::from_utf8_lossy(&translated_result.stderr)
    );
    // At survey coordinates, subtracting nearly equal f64 values during
    // triangulation introduces sub-nanometre-scale roundoff. Compare physical
    // metrics with a documented absolute tolerance, not decimal text equality.
    let translated_report = String::from_utf8(translated_result.stdout).unwrap();
    let translated_lines: Vec<_> = translated_report.trim().lines().collect();
    let reference_lines: Vec<_> = csharp_report.lines().collect();
    assert_eq!(translated_lines.len(), reference_lines.len());
    for (index, (actual, expected)) in translated_lines
        .iter()
        .zip(reference_lines.iter())
        .enumerate()
    {
        if index != 0 {
            assert_eq!(actual, expected, "untranslated walls must remain exact");
            continue;
        }
        let actual_fields: Vec<_> = actual.split(", ").collect();
        let expected_fields: Vec<_> = expected.split(", ").collect();
        assert_eq!(actual_fields.len(), 3);
        assert_eq!(expected_fields.len(), 3);
        assert_eq!(actual_fields[0], expected_fields[0], "wall GUID changed");
        for metric in 1..3 {
            let measured: f64 = actual_fields[metric].parse().unwrap();
            let baseline: f64 = expected_fields[metric].parse().unwrap();
            assert!(
                (measured - baseline).abs() <= 1e-8,
                "translated wall metric {metric} drifted: {measured} vs {baseline}"
            );
        }
    }
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // A well-formed C# JSON envelope with an invalid wall identity must fail
    // before printing any metrics. Preserve the original fixture on disk.
    let mut invalid_guid: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    invalid_guid["Walls"][0]["Id"] = serde_json::json!("not-a-guid");
    let invalid_guid_path = directory.join("invalid-guid-wall.g3d.json");
    fs::write(
        &invalid_guid_path,
        serde_json::to_vec(&invalid_guid).unwrap(),
    )
    .unwrap();
    let invalid_guid_result = Command::new(executable)
        .arg(&invalid_guid_path)
        .output()
        .unwrap();
    assert!(
        !invalid_guid_result.status.success(),
        "Rust must reject a malformed wall GUID in C# serialized JSON"
    );
    assert!(
        invalid_guid_result.stdout.is_empty(),
        "invalid BIM identity must not produce partial metrics"
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // Duplicate a valid wall identity in a copy of the genuine C# JSON.
    // The document must be rejected atomically rather than double-counting
    // a wall or emitting an ambiguous report keyed by the same BIM GUID.
    let mut duplicate_id: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    let first_id = duplicate_id["Walls"][0]["Id"].clone();
    duplicate_id["Walls"][1]["Id"] = first_id;
    let duplicate_id_path = directory.join("duplicate-wall-id.g3d.json");
    fs::write(
        &duplicate_id_path,
        serde_json::to_vec(&duplicate_id).unwrap(),
    )
    .unwrap();
    let duplicate_result = Command::new(executable)
        .arg(&duplicate_id_path)
        .output()
        .unwrap();
    assert!(
        !duplicate_result.status.success(),
        "Rust must reject duplicate wall GUIDs in C# serialized JSON"
    );
    assert!(
        duplicate_result.stdout.is_empty(),
        "duplicate BIM identities must not produce partial metrics"
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // A wall referencing a non-existent level must not be accepted or reported.
    // Change only a copy of the genuine C# serializer output.
    let mut orphan_wall: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    orphan_wall["Walls"][0]["LevelId"] = serde_json::json!("ffffffff-ffff-ffff-ffff-ffffffffffff");
    let orphan_path = directory.join("orphan-level-wall.g3d.json");
    fs::write(&orphan_path, serde_json::to_vec(&orphan_wall).unwrap()).unwrap();
    let orphan_result = Command::new(executable).arg(&orphan_path).output().unwrap();
    assert!(
        !orphan_result.status.success(),
        "Rust must reject a C# wall referencing a missing level"
    );
    assert!(
        orphan_result.stdout.is_empty(),
        "invalid BIM references must not produce partial metrics"
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // A C#-serialized wall with zero thickness is invalid geometry. Reject
    // the entire report without changing the source fixture or emitting a
    // partial report for the other valid walls.
    let mut zero_thickness: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    zero_thickness["Walls"][0]["Thickness"] = serde_json::json!(0.0);
    let zero_thickness_path = directory.join("zero-thickness-wall.g3d.json");
    fs::write(
        &zero_thickness_path,
        serde_json::to_vec(&zero_thickness).unwrap(),
    )
    .unwrap();
    let zero_thickness_result = Command::new(executable)
        .arg(&zero_thickness_path)
        .output()
        .unwrap();
    assert!(
        !zero_thickness_result.status.success(),
        "Rust must reject a zero-thickness C# wall"
    );
    assert!(
        zero_thickness_result.stdout.is_empty(),
        "invalid wall dimensions must not produce partial metrics"
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // A curved wall is not representable by the current straight-wall Rust
    // adapter. Never silently flatten C# geometry into a straight segment.
    let mut curved_wall: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    curved_wall["Walls"][0]["CurveRadius"] = serde_json::json!(2.0);
    let curved_path = directory.join("unsupported-curved-wall.g3d.json");
    fs::write(&curved_path, serde_json::to_vec(&curved_wall).unwrap()).unwrap();
    let curved_result = Command::new(executable).arg(&curved_path).output().unwrap();
    assert!(
        !curved_result.status.success(),
        "Rust must reject unsupported curved C# walls rather than flattening them"
    );
    assert!(
        curved_result.stdout.is_empty(),
        "unsupported C# wall geometry must not produce partial metrics"
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // A C# fixture copy with a door opening must not be treated as a plain
    // solid wall. The adapter must account for the void in both metrics.
    let mut with_door: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    with_door["Openings"] = serde_json::json!([{
        "Id": "00000000-0000-0000-0000-000000000007",
        "WallId": "00000000-0000-0000-0000-000000000002",
        "Kind": 0,
        "Offset": 1.5,
        "Width": 1.0,
        "Sill": 0.0,
        "Height": 2.0
    }]);
    let door_path = directory.join("door-wall.g3d.json");
    fs::write(&door_path, serde_json::to_vec(&with_door).unwrap()).unwrap();
    let door_result = Command::new(executable).arg(&door_path).output().unwrap();
    assert!(
        door_result.status.success(),
        "supported door opening should be inspected: {}",
        String::from_utf8_lossy(&door_result.stderr)
    );
    let door_report = String::from_utf8(door_result.stdout).unwrap();
    let mut door_lines: Vec<String> = csharp_report.lines().map(str::to_owned).collect();
    // A 1 m x 2 m through-opening removes 0.4 m³ and 4 m² of
    // broad-face area; four reveal faces restore 1.2 m².
    door_lines[0] =
        "00000000000000000000000000000002, 2.000000000000, 24.000000000000".to_owned();
    assert_eq!(door_report.trim().replace("\r\n", "\n"), door_lines.join("\n"));
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // A window in the same genuine C#-serialized wall must reduce volume
    // while retaining four reveal faces (including the sill).
    let mut with_window: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    with_window["Openings"] = serde_json::json!([{
        "Id": "00000000-0000-0000-0000-000000000008",
        "WallId": "00000000-0000-0000-0000-000000000002",
        "Kind": 1,
        "Offset": 1.5,
        "Width": 1.0,
        "Sill": 1.0,
        "Height": 1.0
    }]);
    let window_path = directory.join("window-wall.g3d.json");
    fs::write(&window_path, serde_json::to_vec(&with_window).unwrap()).unwrap();
    let window_result = Command::new(executable).arg(&window_path).output().unwrap();
    assert!(
        window_result.status.success(),
        "supported window should be inspected: {}",
        String::from_utf8_lossy(&window_result.stderr)
    );
    let mut window_lines: Vec<String> = csharp_report.lines().map(str::to_owned).collect();
    // Volume: 2.4 - 1*1*0.2 = 2.2 m³.
    // Area: 26.8 - 2*1*1 + 4*1*0.2 = 25.6 m².
    window_lines[0] =
        "00000000000000000000000000000002, 2.200000000000, 25.600000000000".to_owned();
    assert_eq!(
        String::from_utf8(window_result.stdout)
            .unwrap()
            .trim()
            .replace("\r\n", "\n"),
        window_lines.join("\n")
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // Two disjoint windows on the same wall exercise multi-opening topology
    // rather than independent single-opening cases. Edit a fixture copy only.
    let mut two_windows: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    two_windows["Openings"] = serde_json::json!([
        {
            "Id": "00000000-0000-0000-0000-000000000008",
            "WallId": "00000000-0000-0000-0000-000000000002",
            "Kind": 1, "Offset": 1.0, "Width": 0.6, "Sill": 1.0, "Height": 1.0
        },
        {
            "Id": "00000000-0000-0000-0000-000000000009",
            "WallId": "00000000-0000-0000-0000-000000000002",
            "Kind": 1, "Offset": 3.0, "Width": 0.6, "Sill": 1.0, "Height": 1.0
        }
    ]);
    let two_windows_path = directory.join("two-windows-wall.g3d.json");
    fs::write(&two_windows_path, serde_json::to_vec(&two_windows).unwrap()).unwrap();
    let two_windows_result = Command::new(executable)
        .arg(&two_windows_path)
        .output()
        .unwrap();
    assert!(
        two_windows_result.status.success(),
        "two disjoint windows should be supported: {}",
        String::from_utf8_lossy(&two_windows_result.stderr)
    );
    let mut two_windows_lines: Vec<String> = csharp_report.lines().map(str::to_owned).collect();
    // Two 0.6 x 1 m openings remove 0.24 m³ and 2.4 m² from
    // broad faces, while their reveals restore 1.28 m².
    two_windows_lines[0] =
        "00000000000000000000000000000002, 2.160000000000, 25.680000000000"
            .to_owned();
    assert_eq!(
        String::from_utf8(two_windows_result.stdout)
            .unwrap()
            .trim()
            .replace("\r\n", "\n"),
        two_windows_lines.join("\n")
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // Overlapping openings on a real C#-serialized wall are invalid for the
    // current Rust straight-wall mesh contract. Never double-subtract their
    // intersection or emit partial metrics for an invalid BIM document.
    // Only mutate a copy; the C# fixture remains byte-for-byte unchanged.
    let mut overlapping: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    overlapping["Openings"] = serde_json::json!([
        {
            "Id": "00000000-0000-0000-0000-000000000008",
            "WallId": "00000000-0000-0000-0000-000000000002",
            "Kind": 1, "Offset": 1.5, "Width": 1.0, "Sill": 1.0, "Height": 1.0
        },
        {
            "Id": "00000000-0000-0000-0000-000000000009",
            "WallId": "00000000-0000-0000-0000-000000000002",
            "Kind": 1, "Offset": 2.0, "Width": 1.0, "Sill": 1.0, "Height": 1.0
        }
    ]);
    let overlap_path = directory.join("overlapping-windows-wall.g3d.json");
    fs::write(&overlap_path, serde_json::to_vec(&overlapping).unwrap()).unwrap();
    let overlap_result = Command::new(executable).arg(&overlap_path).output().unwrap();
    assert!(
        !overlap_result.status.success(),
        "Rust must reject overlapping C#-serialized openings"
    );
    assert!(
        overlap_result.stdout.is_empty(),
        "overlapping openings must not emit partial wall metrics"
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // C# WallGeometry requires at least 0.05 m between hosted openings.
    // Use two otherwise valid windows with a 0.04 m edge-to-edge gap;
    // Rust must reject the entire copied document without partial output.
    let mut too_close: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    too_close["Openings"] = serde_json::json!([
        {
            "Id": "00000000-0000-0000-0000-000000000008",
            "WallId": "00000000-0000-0000-0000-000000000002",
            "Kind": 1, "Offset": 1.0, "Width": 0.6, "Sill": 1.0, "Height": 1.0
        },
        {
            "Id": "00000000-0000-0000-0000-000000000009",
            "WallId": "00000000-0000-0000-0000-000000000002",
            "Kind": 1, "Offset": 1.64, "Width": 0.6, "Sill": 1.0, "Height": 1.0
        }
    ]);
    let too_close_path = directory.join("too-close-windows-wall.g3d.json");
    fs::write(&too_close_path, serde_json::to_vec(&too_close).unwrap()).unwrap();
    let too_close_result = Command::new(executable)
        .arg(&too_close_path)
        .output()
        .unwrap();
    assert!(
        !too_close_result.status.success(),
        "Rust must reject windows separated by less than 0.05 m"
    );
    assert!(
        too_close_result.stdout.is_empty(),
        "invalid opening clearance must not emit partial metrics"
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // The C# opening discriminator currently accepts only door (0) and
    // window (1). Unknown values must fail closed, never be interpreted
    // as an arbitrary window or silently omitted from wall metrics.
    let mut unknown_kind: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    unknown_kind["Openings"] = serde_json::json!([{
        "Id": "00000000-0000-0000-0000-00000000000a",
        "WallId": "00000000-0000-0000-0000-000000000002",
        "Kind": 2, "Offset": 1.5, "Width": 1.0, "Sill": 1.0, "Height": 1.0
    }]);
    let unknown_kind_path = directory.join("unknown-opening-kind.g3d.json");
    fs::write(&unknown_kind_path, serde_json::to_vec(&unknown_kind).unwrap()).unwrap();
    let unknown_kind_result = Command::new(executable)
        .arg(&unknown_kind_path)
        .output()
        .unwrap();
    assert!(
        !unknown_kind_result.status.success(),
        "Rust must reject unknown C# opening kinds"
    );
    assert!(
        unknown_kind_result.stdout.is_empty(),
        "unknown opening kind must not emit partial wall metrics"
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // Duplicate hosted-opening identities must not be silently accepted.
    // Both windows are geometrically disjoint; identity alone is invalid.
    let mut duplicate_opening: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    duplicate_opening["Openings"] = serde_json::json!([
        {
            "Id": "00000000-0000-0000-0000-000000000008",
            "WallId": "00000000-0000-0000-0000-000000000002",
            "Kind": 1, "Offset": 1.0, "Width": 0.6, "Sill": 1.0, "Height": 1.0
        },
        {
            "Id": "00000000-0000-0000-0000-000000000008",
            "WallId": "00000000-0000-0000-0000-000000000002",
            "Kind": 1, "Offset": 3.0, "Width": 0.6, "Sill": 1.0, "Height": 1.0
        }
    ]);
    let duplicate_opening_path = directory.join("duplicate-opening-id.g3d.json");
    fs::write(
        &duplicate_opening_path,
        serde_json::to_vec(&duplicate_opening).unwrap(),
    )
    .unwrap();
    let duplicate_opening_result = Command::new(executable)
        .arg(&duplicate_opening_path)
        .output()
        .unwrap();
    assert!(
        !duplicate_opening_result.status.success(),
        "Rust must reject duplicate C# hosted-opening GUIDs"
    );
    assert!(
        duplicate_opening_result.stdout.is_empty(),
        "duplicate opening GUIDs must not emit partial wall metrics"
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    // A door and a window on the same wall must compose without counting
    // the two disjoint voids twice. This is still an analytical expectation,
    // not an invocation of production C# WallGeometry.
    let mut mixed: serde_json::Value = serde_json::from_slice(&json_bytes).unwrap();
    mixed["Openings"] = serde_json::json!([
        {
            "Id": "00000000-0000-0000-0000-000000000008",
            "WallId": "00000000-0000-0000-0000-000000000002",
            "Kind": 0, "Offset": 1.0, "Width": 0.8,
            "Sill": 0.0, "Height": 2.0
        },
        {
            "Id": "00000000-0000-0000-0000-000000000009",
            "WallId": "00000000-0000-0000-0000-000000000002",
            "Kind": 1, "Offset": 3.0, "Width": 0.6,
            "Sill": 1.0, "Height": 1.0
        }
    ]);
    let mixed_path = directory.join("mixed-door-window.g3d.json");
    fs::write(&mixed_path, serde_json::to_vec(&mixed).unwrap()).unwrap();
    let mixed_result = Command::new(executable).arg(&mixed_path).output().unwrap();
    assert!(
        mixed_result.status.success(),
        "mixed door/window geometry should be accepted: {}",
        String::from_utf8_lossy(&mixed_result.stderr)
    );
    let mut mixed_lines: Vec<String> = csharp_report.lines().map(str::to_owned).collect();
    // Removed volume: (0.8*2 + 0.6*1)*0.2 = 0.44 m3.
    // Area: 26.8 - 2*(1.6+0.6) + 0.4*(0.8+2+0.6+1) = 24.16 m2.
    mixed_lines[0] =
        "00000000000000000000000000000002, 1.960000000000, 24.160000000000".to_owned();
    assert_eq!(
        String::from_utf8(mixed_result.stdout)
            .unwrap()
            .trim()
            .replace("\r\n", "\n"),
        mixed_lines.join("\n")
    );
    assert_eq!(fs::read(&json_path).unwrap(), json_bytes);

    fs::remove_dir_all(directory).unwrap();
}
