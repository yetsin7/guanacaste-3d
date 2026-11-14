//! Strict JSON escape equivalence at the executable boundary.
//! This is a synthetic regression, not a fixture produced by the C# serializer.
use std::{fs, process::Command, time::{SystemTime, UNIX_EPOCH}};

#[test]
fn escaped_duplicate_wall_property_is_rejected_without_output_or_mutation() {
    let nonce = SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_nanos();
    let path = std::env::temp_dir().join(format!(
        "guanacaste-escaped-duplicate-{}-{nonce}.json", std::process::id()
    ));
    // JSON object names are compared after Unicode escape decoding:
    // "Id" and "\u0049d" denote the same property.
    let input = br#"{"SchemaVersion":15,"Id":"00112233-4455-6677-8899-aabbccddeeff","Name":"test","Levels":[],"Walls":[{"Id":"00000000-0000-0000-0000-000000000002","\u0049d":"00000000-0000-0000-0000-000000000003"}],"Openings":[]}"#;
    fs::write(&path, input).unwrap();
    let output = Command::new(env!("CARGO_BIN_EXE_wall-metrics"))
        .arg(&path).output().unwrap();
    let after = fs::read(&path).unwrap();
    fs::remove_file(&path).unwrap();
    assert_eq!(output.status.code(), Some(1));
    assert!(output.stdout.is_empty(), "invalid JSON emitted metrics");
    assert!(String::from_utf8_lossy(&output.stderr).contains("invalid document envelope"));
    assert_eq!(after, input, "read-only inspection mutated its input");
}
