//! End-to-end fixture produced by the actual C# ProjectFile/NativeDocumentStore.
//! Set GUANACASTE_CSHARP_PARITY=1 to require dotnet 10 and run this contract.
use std::{fs, path::PathBuf, process::Command, time::{SystemTime, UNIX_EPOCH}};

#[test]
fn csharp_serializers_match_rust_wall_metrics() {
    if std::env::var_os("GUANACASTE_CSHARP_PARITY").is_none() {
        eprintln!("C# parity not requested; set GUANACASTE_CSHARP_PARITY=1");
        return;
    }
    let nonce = SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_nanos();
    let directory = std::env::temp_dir().join(format!("g3d-csharp-parity-{}-{nonce}", std::process::id()));
    fs::create_dir(&directory).unwrap();
    let project = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../../interop-fixtures/WallFixture.csproj");
    let generated = Command::new("dotnet")
        .args(["run", "--project"])
        .arg(&project)
        .args(["--configuration", "Release", "--"])
        .arg(&directory)
        .output()
        .expect("dotnet 10 SDK must be installed for C# parity");
    assert!(generated.status.success(), "C# fixture generation failed: {}", String::from_utf8_lossy(&generated.stderr));
    let executable = env!("CARGO_BIN_EXE_wall-metrics");
    let mut reports = Vec::new();
    for filename in ["wall.g3d.json", "wall.g3d"] {
        let path = directory.join(filename);
        let before = fs::read(&path).unwrap();
        let output = Command::new(executable).arg(&path).output().unwrap();
        assert!(output.status.success(), "{filename}: {}", String::from_utf8_lossy(&output.stderr));
        assert_eq!(fs::read(&path).unwrap(), before, "Rust must not mutate C# output");
        reports.push(String::from_utf8(output.stdout).unwrap());
    }
    assert_eq!(reports[0], reports[1], "C# native and JSON outputs differ in Rust");
    // C# fixture has one 4 m x 0.2 m x 3 m wall, no openings.
    assert_eq!(reports[0], "00000000000000000000000000000002, 2.400000000000, 26.800000000000\n");
    fs::remove_dir_all(directory).unwrap();
}
