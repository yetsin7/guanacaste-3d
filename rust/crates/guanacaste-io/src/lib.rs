//! Read-only Guanacaste 3D persistence preflight.
//!
//! Validates the official G3DNATV1 envelope and legacy G3-PROJECT/1 preview.
//! The validated JSON remains opaque: this is NOT a schema migration or a
//! replacement for the full .NET BuildingDocument deserializer.
#![forbid(unsafe_code)]

mod strict_json;

use flate2::read::GzDecoder;
use serde_json::Value;
use sha2::{Digest, Sha256};
use std::io::Read;

pub const MAX_NATIVE_BYTES: usize = 256 * 1024 * 1024;
pub const MAX_PREVIEW_BYTES: usize = 20 * 1024 * 1024;
const SIGNATURE: &[u8; 8] = b"G3DNATV1";
const PREVIEW_SIGNATURE: &[u8] = b"G3-PROJECT/1\n";
const HEADER_LEN: usize = 8 + 4 + 1 + 4 + 16 + 8 + 8 + 32;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum FileError {
    InvalidSignature,
    InvalidLength,
    InvalidVersion,
    InvalidKind,
    InvalidIdentity,
    InvalidPayload,
    InvalidChecksum,
    InvalidJson,
    MissingProjectField,
    DuplicateProjectField,
    Io,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DocumentKind {
    Project,
    Family,
    Template,
}

impl TryFrom<u8> for DocumentKind {
    type Error = FileError;

    fn try_from(value: u8) -> Result<Self, Self::Error> {
        match value {
            1 => Ok(Self::Project),
            2 => Ok(Self::Family),
            3 => Ok(Self::Template),
            _ => Err(FileError::InvalidKind),
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct NativeHeader {
    pub kind: DocumentKind,
    pub schema_version: i32,
    /// Canonical text form of the GUID serialized by .NET Guid.ToByteArray().
    pub document_guid: String,
    pub uncompressed_bytes: usize,
    pub compressed_bytes: usize,
    pub sha256: [u8; 32],
}

/// An inspected document with the exact original JSON payload, never rewritten.
#[derive(Debug, Clone)]
pub struct InspectedDocument {
    pub header: Option<NativeHeader>,
    pub json: Vec<u8>,
}

fn read_i32(bytes: &[u8]) -> i32 {
    i32::from_le_bytes(bytes.try_into().expect("validated fixed slice"))
}
fn read_i64(bytes: &[u8]) -> i64 {
    i64::from_le_bytes(bytes.try_into().expect("validated fixed slice"))
}

/// .NET Guid.ToByteArray serializes the first 4/2/2 bytes little-endian.
fn dotnet_guid(bytes: &[u8]) -> String {
    format!(
        "{:08x}-{:04x}-{:04x}-{:02x}{:02x}-{:02x}{:02x}{:02x}{:02x}{:02x}{:02x}",
        u32::from_le_bytes(bytes[0..4].try_into().expect("four bytes")),
        u16::from_le_bytes(bytes[4..6].try_into().expect("two bytes")),
        u16::from_le_bytes(bytes[6..8].try_into().expect("two bytes")),
        bytes[8],
        bytes[9],
        bytes[10],
        bytes[11],
        bytes[12],
        bytes[13],
        bytes[14],
        bytes[15]
    )
}

fn parse_json(bytes: &[u8]) -> Result<Value, FileError> {
    let root = strict_json::parse(bytes)?;
    if !root.is_object() {
        return Err(FileError::InvalidJson);
    }
    Ok(root)
}

fn validate_project_identity(
    root: &Value,
    schema: Option<i32>,
    guid: Option<&str>,
) -> Result<(), FileError> {
    for field in ["SchemaVersion", "Id", "Name", "Levels", "Walls"] {
        if root.get(field).is_none() {
            return Err(FileError::MissingProjectField);
        }
    }
    let stored_schema = root["SchemaVersion"]
        .as_i64()
        .ok_or(FileError::InvalidPayload)?;
    let stored_guid = root["Id"].as_str().ok_or(FileError::InvalidPayload)?;
    if stored_schema < 1
        || stored_guid.is_empty()
        || stored_guid == "00000000-0000-0000-0000-000000000000"
    {
        return Err(FileError::InvalidIdentity);
    }
    if let Some(schema) = schema {
        if stored_schema != i64::from(schema) {
            return Err(FileError::InvalidIdentity);
        }
    }
    if let Some(guid) = guid {
        if !stored_guid.eq_ignore_ascii_case(guid) {
            return Err(FileError::InvalidIdentity);
        }
    }
    if !root["Levels"].is_array() || !root["Walls"].is_array() || !root["Name"].is_string() {
        return Err(FileError::InvalidPayload);
    }
    Ok(())
}

/// Inspects and verifies all native container bytes without opening a path.
/// A maximum-size limit applies before and after decompression.
pub fn inspect_native(bytes: &[u8]) -> Result<InspectedDocument, FileError> {
    if bytes.len() < HEADER_LEN || bytes.len() > MAX_NATIVE_BYTES + HEADER_LEN {
        return Err(FileError::InvalidLength);
    }
    if &bytes[..8] != SIGNATURE {
        return Err(FileError::InvalidSignature);
    }
    if read_i32(&bytes[8..12]) != 1 {
        return Err(FileError::InvalidVersion);
    }
    let kind = DocumentKind::try_from(bytes[12])?;
    let schema_version = read_i32(&bytes[13..17]);
    let guid_bytes = &bytes[17..33];
    if schema_version < 1 || guid_bytes.iter().all(|b| *b == 0) {
        return Err(FileError::InvalidIdentity);
    }
    let raw_size = read_i64(&bytes[33..41]);
    let zipped_size = read_i64(&bytes[41..49]);
    if raw_size <= 0
        || zipped_size <= 0
        || raw_size > MAX_NATIVE_BYTES as i64
        || zipped_size > MAX_NATIVE_BYTES as i64
        || usize::try_from(zipped_size).ok() != Some(bytes.len() - HEADER_LEN)
    {
        return Err(FileError::InvalidLength);
    }
    let mut expected_hash = [0u8; 32];
    expected_hash.copy_from_slice(&bytes[49..81]);
    let mut json = Vec::new();
    let reader = GzDecoder::new(&bytes[HEADER_LEN..]);
    reader
        .take(raw_size as u64 + 1)
        .read_to_end(&mut json)
        .map_err(|_| FileError::Io)?;
    if json.len() != raw_size as usize {
        return Err(FileError::InvalidLength);
    }
    let actual_hash = Sha256::digest(&json);
    if actual_hash.as_slice() != expected_hash {
        return Err(FileError::InvalidChecksum);
    }
    let root = parse_json(&json)?;
    let document_guid = dotnet_guid(guid_bytes);
    if kind == DocumentKind::Project {
        validate_project_identity(&root, Some(schema_version), Some(&document_guid))?;
    } else {
        if root["SchemaVersion"].as_i64() != Some(i64::from(schema_version))
            || root["Id"]
                .as_str()
                .is_none_or(|id| !id.eq_ignore_ascii_case(&document_guid))
        {
            return Err(FileError::InvalidIdentity);
        }
    }
    Ok(InspectedDocument {
        header: Some(NativeHeader {
            kind,
            schema_version,
            document_guid,
            uncompressed_bytes: raw_size as usize,
            compressed_bytes: zipped_size as usize,
            sha256: expected_hash,
        }),
        json,
    })
}

/// Preflight of the legacy .g3 envelope (not .g3d.json).
/// This does not migrate its schema or rewrite its content.
pub fn inspect_preview(bytes: &[u8]) -> Result<InspectedDocument, FileError> {
    if bytes.len() > MAX_PREVIEW_BYTES {
        return Err(FileError::InvalidLength);
    }
    let json = bytes
        .strip_prefix(PREVIEW_SIGNATURE)
        .ok_or(FileError::InvalidSignature)?;
    let root = parse_json(json)?;
    validate_project_identity(&root, None, None)?;
    Ok(InspectedDocument {
        header: None,
        json: json.to_vec(),
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use flate2::{write::GzEncoder, Compression};
    use std::io::Write;

    const GUID: &str = "00112233-4455-6677-8899-aabbccddeeff";
    fn project_json() -> Vec<u8> {
        format!(
            r#"{{"SchemaVersion":15,"Id":"{GUID}","Name":"Casa de prueba","Levels":[],"Walls":[],"RenderMaterials":[{{"Name":"Preservar"}}]}}"#
        ).into_bytes()
    }
    fn native_with_json(kind: u8, json: &[u8]) -> Vec<u8> {
        let mut encoder = GzEncoder::new(Vec::new(), Compression::default());
        encoder.write_all(json).unwrap();
        let gz = encoder.finish().unwrap();
        let mut data = Vec::new();
        data.extend_from_slice(SIGNATURE);
        data.extend_from_slice(&1i32.to_le_bytes());
        data.push(kind);
        data.extend_from_slice(&15i32.to_le_bytes());
        data.extend_from_slice(&0x00112233u32.to_le_bytes());
        data.extend_from_slice(&0x4455u16.to_le_bytes());
        data.extend_from_slice(&0x6677u16.to_le_bytes());
        data.extend_from_slice(&[0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff]);
        data.extend_from_slice(&(json.len() as i64).to_le_bytes());
        data.extend_from_slice(&(gz.len() as i64).to_le_bytes());
        data.extend_from_slice(Sha256::digest(json).as_slice());
        data.extend_from_slice(&gz);
        data
    }

    #[test]
    fn verifies_native_header_digest_and_preserves_original_json() {
        let original = project_json();
        let result = inspect_native(&native_with_json(1, &original)).unwrap();
        assert_eq!(result.json, original);
        let head = result.header.unwrap();
        assert_eq!(head.kind, DocumentKind::Project);
        assert_eq!(head.document_guid, GUID);
        assert_eq!(head.schema_version, 15);
    }

    #[test]
    fn rejects_changed_checksum_truncated_payload_and_unknown_kind() {
        let original = project_json();
        let mut corrupted = native_with_json(1, &original);
        corrupted[50] ^= 1;
        assert_eq!(
            inspect_native(&corrupted).unwrap_err(),
            FileError::InvalidChecksum
        );
        let mut truncated = native_with_json(1, &original);
        truncated.pop();
        assert_eq!(
            inspect_native(&truncated).unwrap_err(),
            FileError::InvalidLength
        );
        let mut wrong_kind = native_with_json(7, &original);
        wrong_kind[12] = 9;
        assert_eq!(
            inspect_native(&wrong_kind).unwrap_err(),
            FileError::InvalidKind
        );
    }

    #[test]
    fn rejects_payload_identity_mismatch() {
        let json = project_json();
        let mut changed = native_with_json(1, &json);
        changed[17] ^= 1;
        assert_eq!(
            inspect_native(&changed).unwrap_err(),
            FileError::InvalidIdentity
        );
    }

    #[test]
    fn preview_keeps_unknown_fields_and_requires_envelope() {
        let mut content = PREVIEW_SIGNATURE.to_vec();
        content.extend_from_slice(&project_json());
        let parsed = inspect_preview(&content).unwrap();
        assert_eq!(parsed.json, project_json());
        assert_eq!(
            inspect_preview(&project_json()).unwrap_err(),
            FileError::InvalidSignature
        );
    }

    #[test]
    fn rejects_duplicate_fields_in_preview_and_native_payloads() {
        let duplicate_json = format!(
            r#"{{"SchemaVersion":15,"Id":"{GUID}","Name":"Test","Name":"Altered","Levels":[],"Walls":[]}}"#
        );
        let mut preview = PREVIEW_SIGNATURE.to_vec();
        preview.extend_from_slice(duplicate_json.as_bytes());
        assert_eq!(
            inspect_preview(&preview).unwrap_err(),
            FileError::DuplicateProjectField
        );
        let native = native_with_json(1, duplicate_json.as_bytes());
        assert_eq!(
            inspect_native(&native).unwrap_err(),
            FileError::DuplicateProjectField
        );
    }

    #[test]
    fn rejects_missing_project_fields_and_non_object_json() {
        let mut content = PREVIEW_SIGNATURE.to_vec();
        content.extend_from_slice(br#"{"SchemaVersion":15,"Id":"abc"}"#);
        assert_eq!(
            inspect_preview(&content).unwrap_err(),
            FileError::MissingProjectField
        );
        let mut content = PREVIEW_SIGNATURE.to_vec();
        content.extend_from_slice(b"[]");
        assert_eq!(
            inspect_preview(&content).unwrap_err(),
            FileError::InvalidJson
        );
    }
}
