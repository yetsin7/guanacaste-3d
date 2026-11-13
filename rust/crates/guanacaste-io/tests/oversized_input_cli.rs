//! Read-only CLI guard for oversized input files.
use guanacaste_io::MAX_NATIVE_BYTES;
use std::{
    fs,
    process::Command,
    time::{SystemTime, UNIX_EPOCH},
};

#[test]
fn oversized_input_is_rejected_before_parsing_without_mutation() {
    let nonce = SystemTime::now()\n        .duration_since(UNIX_EPOCH)\n        .unwrap()\n        .as_nanos();
    let path = std::env::temp_dir().join(format!(\n        "guanacaste-oversized-{}-{nonce}.g3d",\n        std::process::id()\n    ));
    let file = fs::File::create(&path).unwrap();
    // Sparse extension avoids allocating a 256 MiB fixture on CI runners.
    file.set_len(MAX_NATIVE_BYTES as u64 + 1).unwrap();
    drop(file);
    let output = Command::new(env!("CARGO_BIN_EXE_wall-metrics"))
        .arg(&path)
        .output()
        .unwrap();
    let after = fs::metadata(&path).unwrap().len();
    fs::remove_file(&path).unwrap();
    assert_eq!(output.status.code(), Some(1));
    assert!(output.stdout.is_empty(), "oversized input leaked metrics");
    assert!(
        String::from_utf8_lossy(&output.stderr).contains("input exceeds native document limit")
    );
    assert_eq!(after, MAX_NATIVE_BYTES as u64 + 1);
}
