//! Cross-envelope metrics regression for the read-only schema-15 subset.
//! Synthetic inputs: NOT a fixture emitted by the C# serializer.
use guanacaste_io::{
    bim_slice::{extract_wall_surface_areas, extract_wall_volumes},
    inspect_legacy_json, inspect_preview,
};

const JSON: &str = r#"{
  "SchemaVersion":15,
  "Id":"00112233-4455-6677-8899-aabbccddeeff",
  "Name":"Metrics parity across preview and legacy",
  "Levels":[{"Id":"00000000-0000-0000-0000-000000000001","Name":"L1","Elevation":12.5}],
  "Walls":[{"Id":"00000000-0000-0000-0000-000000000002",
    "LevelId":"00000000-0000-0000-0000-000000000001",
    "Start":{"X":-8,"Y":7},"End":{"X":-4,"Y":7},
    "Thickness":0.2,"Height":3,"BaseOffset":0}],
  "Openings":[{"Id":"00000000-0000-0000-0000-000000000003",
    "WallId":"00000000-0000-0000-0000-000000000002",
    "Kind":1,"Offset":1.5,"Width":1,"Sill":1,"Height":1}],
  "RenderMaterials":[{"KeepUnknown":"unchanged"}]
}"#;

#[test]
fn legacy_and_preview_metrics_match_without_rewriting_json() {
    let legacy = inspect_legacy_json(JSON.as_bytes()).unwrap();
    let mut wrapped = b"G3-PROJECT/1\n".to_vec();
    wrapped.extend_from_slice(JSON.as_bytes());
    let preview = inspect_preview(&wrapped).unwrap();

    assert_eq!(legacy.json, preview.json);
    assert_eq!(legacy.json, JSON.as_bytes());

    let volumes = extract_wall_volumes(&legacy).unwrap();
    let areas = extract_wall_surface_areas(&legacy).unwrap();
    assert_eq!(volumes, extract_wall_volumes(&preview).unwrap());
    assert_eq!(areas, extract_wall_surface_areas(&preview).unwrap());
    assert_eq!(volumes.len(), 1);
    assert_eq!(areas.len(), 1);
    assert_eq!(volumes[0].wall_id, 2);
    assert_eq!(areas[0].wall_id, 2);
    assert!((volumes[0].signed_volume_m3 - 2.2).abs() < 1e-9);
    assert!((areas[0].surface_area_m2 - 25.6).abs() < 1e-9);
}

#[test]
fn native_envelope_metrics_match_legacy_and_preview() {
    use flate2::{write::GzEncoder, Compression};
    use sha2::{Digest, Sha256};
    use std::io::Write;

    let json = JSON.as_bytes();
    let mut encoder = GzEncoder::new(Vec::new(), Compression::default());
    encoder.write_all(json).unwrap();
    let compressed = encoder.finish().unwrap();

    // Header GUID uses .NET Guid.ToByteArray() mixed-endian layout.
    let guid: [u8; 16] = [
        0x33, 0x22, 0x11, 0x00, 0x55, 0x44, 0x77, 0x66, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee,
        0xff,
    ];
    let mut native = Vec::new();
    native.extend_from_slice(b"G3DNATV1");
    native.extend_from_slice(&1i32.to_le_bytes());
    native.push(1); // Project
    native.extend_from_slice(&15i32.to_le_bytes());
    native.extend_from_slice(&guid);
    native.extend_from_slice(&(json.len() as i64).to_le_bytes());
    native.extend_from_slice(&(compressed.len() as i64).to_le_bytes());
    native.extend_from_slice(&Sha256::digest(json));
    native.extend_from_slice(&compressed);

    let inspected = guanacaste_io::inspect_native(&native).unwrap();
    let legacy = inspect_legacy_json(json).unwrap();
    assert_eq!(inspected.json, json);
    assert_eq!(
        inspected.header.as_ref().unwrap().document_guid,
        "00112233-4455-6677-8899-aabbccddeeff"
    );
    assert_eq!(
        extract_wall_volumes(&inspected).unwrap(),
        extract_wall_volumes(&legacy).unwrap()
    );
    assert_eq!(
        extract_wall_surface_areas(&inspected).unwrap(),
        extract_wall_surface_areas(&legacy).unwrap()
    );

    // Corrupted compressed bytes must not be accepted as equivalent geometry.
    let last = native.len() - 1;
    native[last] ^= 0xff;
    assert!(guanacaste_io::inspect_native(&native).is_err());
}
