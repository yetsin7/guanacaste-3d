//! CLI contract regression for wall-metrics. Synthetic input, not C# serializer output.
use std::{
    fs,
    process::Command,
    time::{SystemTime, UNIX_EPOCH},
};

const JSON: &str = r#"{"SchemaVersion":15,"Id":"00112233-4455-6677-8899-aabbccddeeff","Name":"CLI fixture","Levels":[{"Id":"00000000-0000-0000-0000-000000000001","Name":"L1","Elevation":12.5}],"Walls":[{"Id":"00000000-0000-0000-0000-000000000002","LevelId":"00000000-0000-0000-0000-000000000001","Start":{"X":-8,"Y":7},"End":{"X":-4,"Y":7},"Thickness":0.2,"Height":3,"BaseOffset":0}],"Openings":[{"Id":"00000000-0000-0000-0000-000000000003","WallId":"00000000-0000-0000-0000-000000000002","Kind":1,"Offset":1.5,"Width":1,"Sill":1,"Height":1}]}"#;

fn unique_path() -> std::path::PathBuf {
    let nonce = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_nanos();
    std::env::temp_dir().join(format!(
        "guanacaste-wall-metrics-{}-{nonce}.json",
        std::process::id()
    ))
}

#[test]
fn cli_reports_deterministic_metrics_and_rejects_invalid_input() {
    let path = unique_path();
    fs::write(&path, JSON).unwrap();
    let executable = env!("CARGO_BIN_EXE_wall-metrics");
    let output = Command::new(executable).arg(&path).output().unwrap();
    let _ = fs::remove_file(&path);
    assert!(
        output.status.success(),
        "{}",
        String::from_utf8_lossy(&output.stderr)
    );
    assert_eq!(
        String::from_utf8(output.stdout).unwrap(),
        format!("{:032x}, {:.12}, {:.12}\n", 2_u128, 2.2_f64, 25.6_f64)
    );

    let missing = Command::new(executable).output().unwrap();
    assert_eq!(missing.status.code(), Some(2));
    assert!(missing.stdout.is_empty());

    let extra = Command::new(executable)
        .args(["one", "two"])
        .output()
        .unwrap();
    assert_eq!(extra.status.code(), Some(2));
    assert!(extra.stdout.is_empty());
}

#[test]
fn cli_rejects_malformed_json_without_emitting_metrics() {
    let path = unique_path();
    fs::write(&path, b"{ invalid json").unwrap();
    let output = Command::new(env!("CARGO_BIN_EXE_wall-metrics"))
        .arg(&path)
        .output()
        .unwrap();
    let _ = fs::remove_file(&path);
    assert_eq!(output.status.code(), Some(1));
    assert!(output.stdout.is_empty());
    assert!(String::from_utf8_lossy(&output.stderr).contains("invalid document envelope"));
}

#[test]
fn cli_preview_envelope_matches_legacy_json_without_rewriting_input() {
    let executable = env!("CARGO_BIN_EXE_wall-metrics");
    let raw_path = unique_path();
    let preview_path = unique_path();
    let mut preview = b"G3-PROJECT/1\n".to_vec();
    preview.extend_from_slice(JSON.as_bytes());
    fs::write(&raw_path, JSON.as_bytes()).unwrap();
    fs::write(&preview_path, &preview).unwrap();

    let raw = Command::new(executable).arg(&raw_path).output().unwrap();
    let wrapped = Command::new(executable)
        .arg(&preview_path)
        .output()
        .unwrap();
    let raw_after = fs::read(&raw_path).unwrap();
    let preview_after = fs::read(&preview_path).unwrap();
    let _ = fs::remove_file(&raw_path);
    let _ = fs::remove_file(&preview_path);

    assert!(
        raw.status.success(),
        "{}",
        String::from_utf8_lossy(&raw.stderr)
    );
    assert!(
        wrapped.status.success(),
        "{}",
        String::from_utf8_lossy(&wrapped.stderr)
    );
    assert_eq!(raw.stdout, wrapped.stdout);
    assert_eq!(raw_after, JSON.as_bytes());
    assert_eq!(preview_after, preview);
}
