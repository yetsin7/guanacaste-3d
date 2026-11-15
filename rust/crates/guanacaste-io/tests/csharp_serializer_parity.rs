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
    let csharp_report = csharp_report.trim();
    assert_eq!(
        csharp_report,
        concat!(
            "00000000000000000000000000000002, 2.400000000000, 26.800000000000\n",
            "00000000000000000000000000000003, 3.000000000000, 33.200000000000\n",
            "00000000000000000000000000000004, 3.000000000000, 33.200000000000\n",
            "00000000000000000000000000000005, 1.500000000000, 12.700000000000"
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
        reports.push(String::from_utf8(output.stdout).unwrap());
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

    fs::remove_dir_all(directory).unwrap();
}
