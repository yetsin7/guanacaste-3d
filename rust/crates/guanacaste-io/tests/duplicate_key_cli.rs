//! End-to-end read-only safety checks for wall-metrics on invalid schema-15 JSON.
use std::{fs, process::Command, time::{SystemTime, UNIX_EPOCH}};

#[test]
fn duplicate_nested_json_key_is_rejected_without_partial_output_or_mutation() {
    let nonce = SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_nanos();
    let path = std::env::temp_dir().join(format!(
        "guanacaste-duplicate-key-{}-{nonce}.json", std::process::id()
    ));
    // Duplicate nested property names must be rejected before any BIM metric is emitted.
    let input = br#"{"SchemaVersion":15,"Id":"00112233-4455-6677-8899-aabbccddeeff","Name":"test","Levels":[],"Walls":[{"Id":"00000000-0000-0000-0000-000000000002","Id":"00000000-0000-0000-0000-000000000003"}],"Openings":[]}"#;
    fs::write(&path, input).unwrap();
    let output = Command::new(env!("CARGO_BIN_EXE_wall-metrics"))
        .arg(&path).output().unwrap();
    let after = fs::read(&path).unwrap();
    fs::remove_file(&path).unwrap();
    assert_eq!(output.status.code(), Some(1));
    assert!(output.stdout.is_empty(), "invalid document emitted partial metrics");
    assert!(String::from_utf8_lossy(&output.stderr).contains("invalid document envelope"));
    assert_eq!(after, input, "read-only inspection changed the original document");
}
