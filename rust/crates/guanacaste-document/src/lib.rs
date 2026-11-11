//! Early BIM document slice: typed levels, straight walls and hosted openings.
//!
//! Mirrors the geometry contract of BuildingDocument/Wall from .NET, but this
//! is NOT a replacement for the schema-15 document or DocumentSession.
//! Identifiers are stable 128-bit values supplied by the caller (e.g. GUIDs).
#![forbid(unsafe_code)]

use guanacaste_geometry::mesh::TriangleMesh;
use guanacaste_geometry::wall::{mesh_wall_with_openings, WallMeshError, WallOpening};
use std::collections::HashSet;

pub type ElementId = u128;

#[derive(Clone, Debug, PartialEq)]
pub struct Level {
    pub id: ElementId,
    pub name: String,
    pub elevation_m: f64,
}

#[derive(Clone, Debug, PartialEq)]
pub struct Wall {
    pub id: ElementId,
    pub level_id: ElementId,
    pub start: (f64, f64),
    pub end: (f64, f64),
    pub thickness_m: f64,
    /// Height above base level + base offset, in meters.
    pub height_m: f64,
    pub base_offset_m: f64,
}

#[derive(Clone, Debug, PartialEq)]
pub struct HostedOpening {
    pub id: ElementId,
    pub wall_id: ElementId,
    pub from_m: f64,
    pub to_m: f64,
    /// Relative to the wall's actual base elevation.
    pub sill_m: f64,
    pub head_m: f64,
}

#[derive(Clone, Debug, PartialEq, Default)]
pub struct Document {
    pub levels: Vec<Level>,
    pub walls: Vec<Wall>,
    pub openings: Vec<HostedOpening>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum DocumentError {
    DuplicateId,
    EmptyId,
    InvalidLevel,
    MissingLevel,
    InvalidWall,
    MissingWall,
    InvalidOpening,
    Geometry(WallMeshError),
}

impl Document {
    /// Validate references and geometry before building any scene mesh.
    pub fn validate(&self) -> Result<(), DocumentError> {
        let mut ids = HashSet::new();
        for level in &self.levels {
            register_id(&mut ids, level.id)?;
            if level.name.trim().is_empty() || !level.elevation_m.is_finite() {
                return Err(DocumentError::InvalidLevel);
            }
        }
        for wall in &self.walls {
            register_id(&mut ids, wall.id)?;
            if !self.levels.iter().any(|l| l.id == wall.level_id) {
                return Err(DocumentError::MissingLevel);
            }
            let vals = [
                wall.start.0,
                wall.start.1,
                wall.end.0,
                wall.end.1,
                wall.thickness_m,
                wall.height_m,
                wall.base_offset_m,
            ];
            if vals.iter().any(|x| !x.is_finite())
                || wall.height_m <= wall.base_offset_m
                || wall.thickness_m <= 0.0
                || wall.start == wall.end
            {
                return Err(DocumentError::InvalidWall);
            }
        }
        for opening in &self.openings {
            register_id(&mut ids, opening.id)?;
            let Some(wall) = self.walls.iter().find(|w| w.id == opening.wall_id) else {
                return Err(DocumentError::MissingWall);
            };
            let length = (wall.end.0 - wall.start.0).hypot(wall.end.1 - wall.start.1);
            if [opening.from_m, opening.to_m, opening.sill_m, opening.head_m]
                .iter()
                .any(|x| !x.is_finite())
                || opening.from_m < 0.0
                || opening.from_m >= opening.to_m
                || opening.to_m > length
                || opening.sill_m < 0.0
                || opening.sill_m >= opening.head_m
                || opening.head_m > wall.height_m - wall.base_offset_m
            {
                return Err(DocumentError::InvalidOpening);
            }
        }
        for wall in &self.walls {
            // This also checks overlapping hosted openings before commit.
            self.wall_mesh_unchecked(wall)?;
        }
        Ok(())
    }

    /// Stage a complete mutation and commit only if all document invariants hold.
    /// Failed edits leave the current snapshot unchanged.
    pub fn transact(&mut self, edit: impl FnOnce(&mut Document)) -> Result<(), DocumentError> {
        let mut candidate = self.clone();
        edit(&mut candidate);
        candidate.validate()?;
        *self = candidate;
        Ok(())
    }

    /// Build a standalone mesh using typed wall and opening references.
    pub fn wall_mesh(&self, wall_id: ElementId) -> Result<TriangleMesh, DocumentError> {
        self.validate()?;
        let wall = self
            .walls
            .iter()
            .find(|w| w.id == wall_id)
            .ok_or(DocumentError::MissingWall)?;
        self.wall_mesh_unchecked(wall)
    }

    fn wall_mesh_unchecked(&self, wall: &Wall) -> Result<TriangleMesh, DocumentError> {
        let level = self
            .levels
            .iter()
            .find(|l| l.id == wall.level_id)
            .ok_or(DocumentError::MissingLevel)?;
        let bottom = level.elevation_m + wall.base_offset_m;
        let top = level.elevation_m + wall.height_m;
        if !bottom.is_finite() || !top.is_finite() {
            return Err(DocumentError::InvalidWall);
        }
        let mut holes = Vec::new();
        for opening in self.openings.iter().filter(|o| o.wall_id == wall.id) {
            holes.push(WallOpening {
                from: opening.from_m,
                to: opening.to_m,
                bottom: bottom + opening.sill_m,
                top: bottom + opening.head_m,
            });
        }
        mesh_wall_with_openings(wall.start, wall.end, bottom, top, wall.thickness_m, &holes)
            .map_err(DocumentError::Geometry)
    }
}

fn register_id(ids: &mut HashSet<ElementId>, id: ElementId) -> Result<(), DocumentError> {
    if id == 0 {
        return Err(DocumentError::EmptyId);
    }
    if !ids.insert(id) {
        return Err(DocumentError::DuplicateId);
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn sample() -> Document {
        Document {
            levels: vec![Level {
                id: 1,
                name: "Level 1".into(),
                elevation_m: 2.0,
            }],
            walls: vec![Wall {
                id: 2,
                level_id: 1,
                start: (0.0, 0.0),
                end: (4.0, 0.0),
                thickness_m: 0.2,
                height_m: 3.0,
                base_offset_m: 0.5,
            }],
            openings: vec![HostedOpening {
                id: 3,
                wall_id: 2,
                from_m: 1.0,
                to_m: 2.0,
                sill_m: 0.5,
                head_m: 1.5,
            }],
        }
    }

    #[test]
    fn builds_hosted_wall_using_level_and_base_offset() {
        let doc = sample();
        doc.validate().unwrap();
        let mesh = doc.wall_mesh(2).unwrap();
        let (min, max) = mesh.bounds().unwrap();
        assert_eq!(min.z, 2.5);
        assert_eq!(max.z, 5.0);
        assert!((mesh.signed_volume() - 1.8).abs() < 1e-9);
    }

    #[test]
    fn rejects_unknown_host_and_duplicate_ids() {
        let mut doc = sample();
        doc.openings[0].wall_id = 500;
        assert_eq!(doc.validate(), Err(DocumentError::MissingWall));
        doc.openings[0].wall_id = 2;
        doc.openings[0].id = 2;
        assert_eq!(doc.validate(), Err(DocumentError::DuplicateId));
    }

    #[test]
    fn transaction_rejects_invalid_mutation_without_changing_snapshot() {
        let mut doc = sample();
        let before = doc.clone();
        assert_eq!(
            doc.transact(|candidate| candidate.walls[0].thickness_m = -1.0),
            Err(DocumentError::InvalidWall)
        );
        assert_eq!(doc, before);
        doc.transact(|candidate| candidate.walls[0].thickness_m = 0.3)
            .unwrap();
        assert_eq!(doc.walls[0].thickness_m, 0.3);
    }

    #[test]
    fn overlapping_openings_are_rejected_transactionally() {
        let mut doc = sample();
        let before = doc.clone();
        let result = doc.transact(|candidate| {
            candidate.openings.push(HostedOpening {
                id: 4,
                wall_id: 2,
                from_m: 1.5,
                to_m: 2.5,
                sill_m: 0.75,
                head_m: 1.75,
            });
        });
        assert_eq!(
            result,
            Err(DocumentError::Geometry(WallMeshError::OverlappingOpenings))
        );
        assert_eq!(doc, before);
    }
}
