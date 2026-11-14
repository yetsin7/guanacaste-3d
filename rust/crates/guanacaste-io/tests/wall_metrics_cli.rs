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

#[test]
fn cli_native_envelope_matches_legacy_and_rejects_tampering() {
    use flate2::{write::GzEncoder, Compression};
    use sha2::{Digest, Sha256};
    use std::io::Write;

    let executable = env!("CARGO_BIN_EXE_wall-metrics");
    let raw_path = unique_path();
    let native_path = unique_path();
    let json = JSON.as_bytes();
    let mut encoder = GzEncoder::new(Vec::new(), Compression::default());
    encoder.write_all(json).unwrap();
    let compressed = encoder.finish().unwrap();
    // Guid.ToByteArray() uses mixed-endian layout in the native header.
    let guid: [u8; 16] = [
        0x33, 0x22, 0x11, 0x00, 0x55, 0x44, 0x77, 0x66, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee,
        0xff,
    ];
    let mut native = Vec::new();
    native.extend_from_slice(b"G3DNATV1");
    native.extend_from_slice(&1i32.to_le_bytes());
    native.push(1);
    native.extend_from_slice(&15i32.to_le_bytes());
    native.extend_from_slice(&guid);
    native.extend_from_slice(&(json.len() as i64).to_le_bytes());
    native.extend_from_slice(&(compressed.len() as i64).to_le_bytes());
    native.extend_from_slice(&Sha256::digest(json));
    native.extend_from_slice(&compressed);
    fs::write(&raw_path, json).unwrap();
    fs::write(&native_path, &native).unwrap();

    let raw = Command::new(executable).arg(&raw_path).output().unwrap();
    let wrapped = Command::new(executable).arg(&native_path).output().unwrap();
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
    assert_eq!(fs::read(&raw_path).unwrap(), json);
    assert_eq!(fs::read(&native_path).unwrap(), native);

    // Flip a digest byte: integrity failure must not print potentially misleading metrics.
    native[49] ^= 0xff;
    fs::write(&native_path, &native).unwrap();
    let corrupted = Command::new(executable).arg(&native_path).output().unwrap();
    assert_eq!(corrupted.status.code(), Some(1));
    assert!(corrupted.stdout.is_empty());
    assert!(String::from_utf8_lossy(&corrupted.stderr).contains("invalid document envelope"));
    let _ = fs::remove_file(&raw_path);
    let _ = fs::remove_file(&native_path);
}

#[test]
fn cli_emits_metrics_for_every_wall() {
    let mut document: serde_json::Value = serde_json::from_str(JSON).unwrap();
    let mut second = document["Walls"][0].clone();
    second["Id"] = serde_json::Value::String("00000000-0000-0000-0000-000000000004".to_string());
    second["Start"]["Y"] = serde_json::json!(12);
    second["End"]["Y"] = serde_json::json!(12);
    document["Walls"].as_array_mut().unwrap().push(second);
    let path = unique_path();
    fs::write(&path, serde_json::to_vec(&document).unwrap()).unwrap();
    let output = Command::new(env!("CARGO_BIN_EXE_wall-metrics"))
        .arg(&path)
        .output()
        .unwrap();
    let _ = fs::remove_file(&path);
    assert!(
        output.status.success(),
        "{}",
        String::from_utf8_lossy(&output.stderr)
    );
    let stdout = String::from_utf8(output.stdout).unwrap();
    let lines: Vec<_> = stdout.lines().collect();
    assert_eq!(lines.len(), 2, "{stdout}");
    assert!(lines
        .iter()
        .any(|line| line.starts_with("00000000000000000000000000000002,")));
    assert!(lines
        .iter()
        .any(|line| line.starts_with("00000000000000000000000000000004,")));
}

#[test]
fn cli_does_not_emit_partial_metrics_when_a_later_wall_is_invalid() {
    let mut document: serde_json::Value = serde_json::from_str(JSON).unwrap();
    let mut second = document["Walls"][0].clone();
    second["Id"] = serde_json::json!("00000000-0000-0000-0000-000000000004");
    second["Thickness"] = serde_json::json!(-0.2);
    document["Walls"].as_array_mut().unwrap().push(second);

    let path = unique_path();
    let original = serde_json::to_vec(&document).unwrap();
    fs::write(&path, &original).unwrap();
    let output = Command::new(env!("CARGO_BIN_EXE_wall-metrics"))
        .arg(&path)
        .output()
        .unwrap();
    let after = fs::read(&path).unwrap();
    let _ = fs::remove_file(&path);

    assert_eq!(output.status.code(), Some(1));
    assert!(
        output.stdout.is_empty(),
        "partial metrics escaped on failure"
    );
    assert!(String::from_utf8_lossy(&output.stderr).contains("unsupported wall subset"));
    assert_eq!(after, original, "inspection must never rewrite input");
}

#[test]
fn cli_rejects_a_later_invalid_wall_guid_without_partial_output() {
    let mut document: serde_json::Value = serde_json::from_str(JSON).unwrap();
    let mut second = document["Walls"][0].clone();
    second["Id"] = serde_json::json!("not-a-guid");
    document["Walls"].as_array_mut().unwrap().push(second);

    let path = unique_path();
    let original = serde_json::to_vec(&document).unwrap();
    fs::write(&path, &original).unwrap();
    let output = Command::new(env!("CARGO_BIN_EXE_wall-metrics"))
        .arg(&path)
        .output()
        .unwrap();
    let after = fs::read(&path).unwrap();
    let _ = fs::remove_file(&path);

    assert_eq!(output.status.code(), Some(1));
    assert!(output.stdout.is_empty(), "invalid later GUID leaked metrics");
    assert_eq!(after, original, "read-only CLI must preserve input");
}
