//! CLI regression: a later unsupported curved wall must not leak earlier metrics.
//! Synthetic input; this is not a C# serializer parity fixture.
use std::{fs, process::Command};

#[test]
fn later_curved_wall_rejects_entire_report_without_mutating_document() {
    let mut document = serde_json::json!({
        "SchemaVersion": 15,
        "Id": "00112233-4455-6677-8899-aabbccddeeff",
        "Name": "Curved wall rejection",
        "Levels": [{
            "Id": "00000000-0000-0000-0000-000000000001",
            "Name": "Level 1",
            "Elevation": 0
        }],
        "Walls": [{
            "Id": "00000000-0000-0000-0000-000000000002",
            "LevelId": "00000000-0000-0000-0000-000000000001",
            "Start": {"X": 0, "Y": 0},
            "End": {"X": 4, "Y": 0},
            "Thickness": 0.2,
            "Height": 3,
            "BaseOffset": 0
        }],
        "Openings": []
    });
    let mut curved = document["Walls"][0].clone();
    curved["Id"] = serde_json::json!("00000000-0000-0000-0000-000000000003");
    curved["CurveRadius"] = serde_json::json!(2.0);
    document["Walls"].as_array_mut().unwrap().push(curved);

    let original = serde_json::to_vec(&document).unwrap();
    let path = std::env::temp_dir().join(format!(
        "guanacaste-curved-wall-{}-{}.json",
        std::process::id(),
        std::thread::current().name().unwrap_or("test")
    ));
    fs::write(&path, &original).unwrap();
    let output = Command::new(env!("CARGO_BIN_EXE_wall-metrics"))
        .arg(&path)
        .output()
        .unwrap();
    let unchanged = fs::read(&path).unwrap();
    fs::remove_file(&path).unwrap();

    assert_eq!(output.status.code(), Some(1));
    assert!(output.stdout.is_empty(), "unsupported geometry leaked metrics");
    assert!(
        String::from_utf8_lossy(&output.stderr).contains("unsupported wall subset"),
        "unexpected error: {}",
        String::from_utf8_lossy(&output.stderr)
    );
    assert_eq!(unchanged, original);
}
