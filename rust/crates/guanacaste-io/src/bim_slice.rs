//! Narrow, read-only schema-15 adapter for straight, level-hosted wall geometry.
//! This is intentionally not a full BuildingDocument loader: unsupported wall
//! profiles, top-level constraints or curves are rejected, never flattened.
#![forbid(unsafe_code)]

use guanacaste_document::{Document, DocumentError, HostedOpening, Level, Wall};
use serde_json::Value;

use crate::{strict_json, DocumentKind, InspectedDocument};

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SliceError {
    NotProject,
    InvalidJson,
    UnsupportedSchema,
    UnsupportedWallFeature,
    InvalidField,
    InvalidGuid,
    InvalidOpeningRules,
    InvalidDocument(DocumentError),
}

impl From<DocumentError> for SliceError {
    fn from(value: DocumentError) -> Self {
        Self::InvalidDocument(value)
    }
}

/// Decode exactly the supported subset of a *previously inspected* file.
/// The caller retains the original inspected JSON, including unknown fields.
pub fn extract_wall_slice(input: &InspectedDocument) -> Result<Document, SliceError> {
    if input
        .header
        .as_ref()
        .is_some_and(|header| header.kind != DocumentKind::Project)
    {
        return Err(SliceError::NotProject);
    }
    let root = strict_json::parse(&input.json).map_err(|_| SliceError::InvalidJson)?;
    if root.get("SchemaVersion").and_then(Value::as_i64) != Some(15) {
        return Err(SliceError::UnsupportedSchema);
    }
    let levels = array(&root, "Levels")?
        .iter()
        .map(|level| {
            Ok(Level {
                id: guid(level, "Id")?,
                name: string(level, "Name")?.to_owned(),
                elevation_m: number(level, "Elevation")?,
            })
        })
        .collect::<Result<Vec<_>, SliceError>>()?;
    let walls = array(&root, "Walls")?
        .iter()
        .map(|wall| {
            // These C# features require different extrusion and elevation rules.
            if ["TopProfile", "TopLevelId", "CurveGroupId"]
                .iter()
                .any(|key| wall.get(*key).is_some_and(|v| !v.is_null()))
                || wall
                    .get("CurveRadius")
                    .is_some_and(|v| v.as_f64() != Some(0.0))
                || wall
                    .get("TopOffset")
                    .is_some_and(|v| v.as_f64() != Some(0.0))
            {
                return Err(SliceError::UnsupportedWallFeature);
            }
            Ok(Wall {
                id: guid(wall, "Id")?,
                level_id: guid(wall, "LevelId")?,
                start: point(wall, "Start")?,
                end: point(wall, "End")?,
                thickness_m: number(wall, "Thickness")?,
                height_m: number(wall, "Height")?,
                base_offset_m: optional_number(wall, "BaseOffset")?,
            })
        })
        .collect::<Result<Vec<_>, SliceError>>()?;
    let openings = match root.get("Openings") {
        None => Vec::new(),
        Some(value) => value
            .as_array()
            .ok_or(SliceError::InvalidField)?
            .iter()
            .map(|opening| {
                HostedOpening::from_dotnet_dimensions(
                    guid(opening, "Id")?,
                    guid(opening, "WallId")?,
                    number(opening, "Offset")?,
                    number(opening, "Width")?,
                    number(opening, "Sill")?,
                    number(opening, "Height")?,
                )
                .map_err(SliceError::InvalidDocument)
            })
            .collect::<Result<Vec<_>, SliceError>>()?,
    };
    validate_dotnet_opening_rules(&walls, &openings, &root)?;
    let doc = Document {
        levels,
        walls,
        openings,
    };
    doc.validate()?;
    Ok(doc)
}

/// Deterministic, read-only per-wall volume report for future C# golden fixtures.
/// IDs are canonical numeric GUIDs; volumes use cubic metres and signed mesh
/// orientation. This does not imply parity with the .NET geometry engine.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct WallVolume {
    pub wall_id: u128,
    pub signed_volume_m3: f64,
}

pub fn extract_wall_volumes(input: &InspectedDocument) -> Result<Vec<WallVolume>, SliceError> {
    let document = extract_wall_slice(input)?;
    let mut result = Vec::with_capacity(document.walls.len());
    for wall in &document.walls {
        let mesh = document.wall_mesh(wall.id)?;
        result.push(WallVolume {
            wall_id: wall.id,
            signed_volume_m3: mesh.signed_volume(),
        });
    }
    result.sort_unstable_by_key(|item| item.wall_id);
    Ok(result)
}

/// Read-only surface-area report for the same supported wall subset.
/// Areas are measured in square metres and sorted by canonical GUID.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct WallSurfaceArea {
    pub wall_id: u128,
    pub surface_area_m2: f64,
}

pub fn extract_wall_surface_areas(
    input: &InspectedDocument,
) -> Result<Vec<WallSurfaceArea>, SliceError> {
    let document = extract_wall_slice(input)?;
    let mut result = Vec::with_capacity(document.walls.len());
    for wall in &document.walls {
        let mesh = document.wall_mesh(wall.id)?;
        let area = mesh
            .checked_closed_surface_area()
            .ok_or(SliceError::InvalidField)?;
        result.push(WallSurfaceArea {
            wall_id: wall.id,
            surface_area_m2: area,
        });
    }
    result.sort_unstable_by_key(|item| item.wall_id);
    Ok(result)
}

/// Mirrors the subset of C# WallGeometry.Validate that depends on straight,
/// flat-top wall and opening dimensions. The geometry crate deliberately keeps
/// its own lower-level invariants; the persistence adapter applies BIM rules.
fn validate_dotnet_opening_rules(
    walls: &[Wall],
    openings: &[HostedOpening],
    root: &Value,
) -> Result<(), SliceError> {
    let raw = match root.get("Openings") {
        None => return Ok(()),
        Some(value) => value.as_array().ok_or(SliceError::InvalidField)?,
    };
    if raw.len() > 200_000 {
        return Err(SliceError::InvalidOpeningRules);
    }
    for (item, opening) in raw.iter().zip(openings) {
        let kind = item.get("Kind").and_then(Value::as_i64);
        if !matches!(kind, Some(0 | 1)) {
            return Err(SliceError::InvalidOpeningRules);
        }
        let wall = walls
            .iter()
            .find(|wall| wall.id == opening.wall_id)
            .ok_or(SliceError::InvalidDocument(DocumentError::MissingWall))?;
        let length = (wall.end.0 - wall.start.0).hypot(wall.end.1 - wall.start.1);
        if opening.to_m - opening.from_m < 0.3
            || opening.head_m - opening.sill_m < 0.3
            || opening.from_m < 0.05
            || opening.to_m > length - 0.05
            || opening.head_m > wall.height_m - 0.05
            || (kind == Some(0) && opening.sill_m != 0.0)
        {
            return Err(SliceError::InvalidOpeningRules);
        }
    }
    // C# rejects gaps below 0.05 m between openings on the same wall.
    // Sorting by host and interval avoids the quadratic pairwise scan on
    // large imported projects. Adjacent intervals are sufficient to detect
    // every overlap or undersized gap in a sorted sequence.
    let mut ordered: Vec<_> = openings.iter().collect();
    ordered.sort_unstable_by(|a, b| {
        a.wall_id
            .cmp(&b.wall_id)
            .then_with(|| a.from_m.total_cmp(&b.from_m))
    });
    for pair in ordered.windows(2) {
        if pair[0].wall_id == pair[1].wall_id && pair[1].from_m < pair[0].to_m + 0.05 {
            return Err(SliceError::InvalidOpeningRules);
        }
    }
    Ok(())
}

fn array<'a>(root: &'a Value, key: &str) -> Result<&'a Vec<Value>, SliceError> {
    root.get(key)
        .and_then(Value::as_array)
        .ok_or(SliceError::InvalidField)
}

fn string<'a>(root: &'a Value, key: &str) -> Result<&'a str, SliceError> {
    root.get(key)
        .and_then(Value::as_str)
        .ok_or(SliceError::InvalidField)
}

fn number(root: &Value, key: &str) -> Result<f64, SliceError> {
    let value = root
        .get(key)
        .and_then(Value::as_f64)
        .ok_or(SliceError::InvalidField)?;
    if !value.is_finite() {
        return Err(SliceError::InvalidField);
    }
    Ok(value)
}

fn optional_number(root: &Value, key: &str) -> Result<f64, SliceError> {
    match root.get(key) {
        None => Ok(0.0),
        Some(_) => number(root, key),
    }
}

fn point(root: &Value, key: &str) -> Result<(f64, f64), SliceError> {
    let point = root.get(key).ok_or(SliceError::InvalidField)?;
    Ok((number(point, "X")?, number(point, "Y")?))
}

/// Parse the canonical 8-4-4-4-12 GUID notation into a stable 128-bit ID.
/// The numeric value uses canonical textual order, not .NET's mixed-endian bytes.
fn guid(root: &Value, key: &str) -> Result<u128, SliceError> {
    let value = string(root, key)?;
    let bytes = value.as_bytes();
    if bytes.len() != 36 || [8, 13, 18, 23].iter().any(|&index| bytes[index] != b'-') {
        return Err(SliceError::InvalidGuid);
    }
    let mut value = 0u128;
    for (index, byte) in bytes.iter().enumerate() {
        if [8, 13, 18, 23].contains(&index) {
            continue;
        }
        let digit = (*byte as char)
            .to_digit(16)
            .ok_or(SliceError::InvalidGuid)?;
        value = (value << 4) | u128::from(digit);
    }
    if value == 0 {
        return Err(SliceError::InvalidGuid);
    }
    Ok(value)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::inspect_legacy_json;

    const PROJECT: &str = r#"{
      "SchemaVersion":15,
      "Id":"00112233-4455-6677-8899-aabbccddeeff",
      "Name":"Test BIM",
      "Levels":[{"Id":"00000000-0000-0000-0000-000000000001","Name":"Planta","Elevation":2.0}],
      "Walls":[{"Id":"00000000-0000-0000-0000-000000000002",
        "LevelId":"00000000-0000-0000-0000-000000000001",
        "Start":{"X":0,"Y":0},"End":{"X":4,"Y":0},
        "Thickness":0.2,"Height":3,"BaseOffset":0.5}],
      "Openings":[{"Id":"00000000-0000-0000-0000-000000000003",
        "WallId":"00000000-0000-0000-0000-000000000002",
        "Kind":1,"Offset":1.5,"Width":1,"Sill":1,"Height":1}],
      "RenderMaterials":[{"CustomSetting":"preserve-original"}]
    }"#;

    #[test]
    fn extracts_typed_wall_and_opening_without_mutating_native_json() {
        let inspected = inspect_legacy_json(PROJECT.as_bytes()).unwrap();
        let original = inspected.json.clone();
        let slice = extract_wall_slice(&inspected).unwrap();
        assert_eq!(slice.levels[0].id, 1);
        assert_eq!(slice.walls[0].id, 2);
        assert_eq!(slice.openings[0].from_m, 1.0);
        assert_eq!(slice.openings[0].to_m, 2.0);
        assert!((slice.wall_mesh(2).unwrap().signed_volume() - 1.8).abs() < 1e-9);
        assert_eq!(inspected.json, original);
    }

    #[test]
    fn produces_deterministic_wall_volumes_without_changing_original_payload() {
        let inspected = inspect_legacy_json(PROJECT.as_bytes()).unwrap();
        let original = inspected.json.clone();
        let volumes = extract_wall_volumes(&inspected).unwrap();
        assert_eq!(volumes.len(), 1);
        assert_eq!(volumes[0].wall_id, 2);
        assert!((volumes[0].signed_volume_m3 - 1.8).abs() < 1e-9);
        assert_eq!(inspected.json, original);
    }

    #[test]
    fn wall_volume_results_are_sorted_by_guid_not_document_order() {
        let mut payload: Value = serde_json::from_str(PROJECT).unwrap();
        let mut second = payload["Walls"][0].clone();
        second["Id"] = Value::from("00000000-0000-0000-0000-000000000004");
        second["Start"]["Y"] = Value::from(5.0);
        second["End"]["Y"] = Value::from(5.0);
        // The second wall is serialized before the first one on purpose.
        payload["Walls"] = Value::from(vec![second, payload["Walls"][0].clone()]);
        let json = serde_json::to_vec(&payload).unwrap();
        let inspected = inspect_legacy_json(&json).unwrap();
        let volumes = extract_wall_volumes(&inspected).unwrap();
        assert_eq!(volumes.len(), 2);
        assert_eq!(volumes[0].wall_id, 2);
        assert_eq!(volumes[1].wall_id, 4);
        assert!((volumes[0].signed_volume_m3 - 1.8).abs() < 1e-9);
        assert!((volumes[1].signed_volume_m3 - 2.0).abs() < 1e-9);
        assert_eq!(inspected.json, json);
    }

    #[test]
    fn surface_area_report_is_deterministic_and_preserves_json() {
        let inspected = inspect_legacy_json(PROJECT.as_bytes()).unwrap();
        let original = inspected.json.clone();
        let areas = extract_wall_surface_areas(&inspected).unwrap();
        assert_eq!(areas.len(), 1);
        assert_eq!(areas[0].wall_id, 2);
        // Height=3 is relative to the level, while BaseOffset=0.5 raises
        // the wall base: its actual solid height is 2.5 m, not 3 m.
        // 2*(4*2.5 + 4*0.2 + 2.5*0.2) - 2*(1*1) + 2*(1+1)*0.2 = 21.4 m².
        assert!((areas[0].surface_area_m2 - 21.4).abs() < 1e-9);
        assert_eq!(inspected.json, original);
    }

    #[test]
    fn wall_surface_area_uses_level_relative_top_and_raised_base() {
        let mut payload: Value = serde_json::from_str(PROJECT).unwrap();
        payload["Walls"][0]["BaseOffset"] = Value::from(0.0);
        let json = serde_json::to_vec(&payload).unwrap();
        let inspected = inspect_legacy_json(&json).unwrap();
        let areas = extract_wall_surface_areas(&inspected).unwrap();
        assert_eq!(areas.len(), 1);
        // A full 3 m wall with the same through-opening has area 25.6 m².
        assert!((areas[0].surface_area_m2 - 25.6).abs() < 1e-9);
        assert_eq!(inspected.json, json);
    }

    #[test]
    fn refuses_unsupported_curves_without_silently_flattening_them() {
        let curved = PROJECT.replace(
            "\"BaseOffset\":0.5",
            "\"BaseOffset\":0.5,\"CurveRadius\":5.0",
        );
        let inspected = inspect_legacy_json(curved.as_bytes()).unwrap();
        assert_eq!(
            extract_wall_slice(&inspected),
            Err(SliceError::UnsupportedWallFeature)
        );
    }

    #[test]
    fn refuses_stale_schemas_and_unhosted_openings() {
        let old = PROJECT.replace("\"SchemaVersion\":15", "\"SchemaVersion\":14");
        let inspected = inspect_legacy_json(old.as_bytes()).unwrap();
        assert_eq!(
            extract_wall_slice(&inspected),
            Err(SliceError::UnsupportedSchema)
        );
        let missing = PROJECT.replace(
            "\"WallId\":\"00000000-0000-0000-0000-000000000002\"",
            "\"WallId\":\"00000000-0000-0000-0000-000000000009\"",
        );
        let inspected = inspect_legacy_json(missing.as_bytes()).unwrap();
        assert_eq!(
            extract_wall_slice(&inspected),
            Err(SliceError::InvalidDocument(DocumentError::MissingWall))
        );
    }

    #[test]
    fn enforces_csharp_wall_geometry_opening_clearances_and_door_sills() {
        let cases = [
            ("\"Width\":1", "\"Width\":0.2"),
            ("\"Height\":1}", "\"Height\":0.2}"),
            ("\"Offset\":1.5", "\"Offset\":0.5"),
            ("\"Sill\":1", "\"Sill\":2.1"),
            ("\"Kind\":1", "\"Kind\":0"),
            ("\"Kind\":1", "\"Kind\":99"),
        ];
        for (from, to) in cases {
            let payload = PROJECT.replace(from, to);
            let inspected = inspect_legacy_json(payload.as_bytes()).unwrap();
            assert_eq!(
                extract_wall_slice(&inspected),
                Err(SliceError::InvalidOpeningRules),
                "unexpected acceptance: {to}"
            );
        }
    }

    #[test]
    fn rejects_nearby_openings_even_when_they_do_not_overlap() {
        let extra = r#",{"Id":"00000000-0000-0000-0000-000000000004",
            "WallId":"00000000-0000-0000-0000-000000000002",
            "Kind":1,"Offset":2.54,"Width":1,"Sill":1,"Height":1}"#;
        let payload = PROJECT.replace(
            "\"Sill\":1,\"Height\":1}],",
            &format!("\"Sill\":1,\"Height\":1}}{extra}],"),
        );
        let inspected = inspect_legacy_json(payload.as_bytes()).unwrap();
        assert_eq!(
            extract_wall_slice(&inspected),
            Err(SliceError::InvalidOpeningRules)
        );
    }

    #[test]
    fn opening_clearances_are_checked_per_host_even_when_input_is_unsorted() {
        let mut payload: Value = serde_json::from_str(PROJECT).unwrap();
        let original = payload["Openings"][0].clone();
        let mut adjacent = original.clone();
        adjacent["Id"] = Value::from("00000000-0000-0000-0000-000000000004");
        adjacent["Offset"] = Value::from(2.54);
        payload["Openings"] = Value::from(vec![adjacent, original]);
        let json = serde_json::to_vec(&payload).unwrap();
        let inspected = inspect_legacy_json(&json).unwrap();
        assert_eq!(
            extract_wall_slice(&inspected),
            Err(SliceError::InvalidOpeningRules)
        );

        // C# permits a gap of at least 0.05 m between openings.
        payload["Openings"][0]["Offset"] = Value::from(2.55);
        let json = serde_json::to_vec(&payload).unwrap();
        let inspected = inspect_legacy_json(&json).unwrap();
        assert!(extract_wall_slice(&inspected).is_ok());
    }

    #[test]
    fn rejects_invalid_guid_and_duplicate_ids() {
        let bad = PROJECT.replace(
            "\"Id\":\"00000000-0000-0000-0000-000000000001\"",
            "\"Id\":\"not-a-guid\"",
        );
        let inspected = inspect_legacy_json(bad.as_bytes()).unwrap();
        assert_eq!(extract_wall_slice(&inspected), Err(SliceError::InvalidGuid));
        let duplicate = PROJECT.replace(
            "\"Id\":\"00000000-0000-0000-0000-000000000003\"",
            "\"Id\":\"00000000-0000-0000-0000-000000000002\"",
        );
        let inspected = inspect_legacy_json(duplicate.as_bytes()).unwrap();
        assert_eq!(
            extract_wall_slice(&inspected),
            Err(SliceError::InvalidDocument(DocumentError::DuplicateId))
        );
    }
}
