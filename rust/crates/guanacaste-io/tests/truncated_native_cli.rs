//! Native envelope truncation must fail atomically without rewriting source bytes.
//! Synthetic regression; not evidence of C# serializer parity.
use std::{fs, process::Command, time::{SystemTime, UNIX_EPOCH}};

#[test]
fn truncated_native_header_does_not_emit_metrics_or_modify_input() {
    let nonce = SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_nanos();
    let path = std::env::temp_dir().join(format!(
        "guanacaste-truncated-native-{}-{nonce}.g3d", std::process::id()
    ));
    // Correct magic, deliberately incomplete native header.
    let original = b"G3DNATV1";
    fs::write(&path, original).unwrap();
    let result = Command::new(env!("CARGO_BIN_EXE_wall-metrics"))
        .arg(&path)
        .output()
        .unwrap();
    let after = fs::read(&path).unwrap();
    fs::remove_file(&path).unwrap();
    assert_eq!(result.status.code(), Some(1));
    assert!(result.stdout.is_empty(), "invalid native header emitted partial metrics");
    assert!(String::from_utf8_lossy(&result.stderr).contains("invalid document envelope"));
    assert_eq!(after, original, "inspection must not rewrite native files");
}
