//! Rectangular wall solids with through-openings, in metric Z-up coordinates.
//! A conforming u/z cell grid ensures opening jambs and wall faces share edges.
//! This is a dedicated geometry primitive, not yet a replacement for
//! `WallGeometry.Solids`, angled/sloped walls or BIM join resolution.
#![forbid(unsafe_code)]

use crate::mesh::{Point3, TriangleMesh};
use std::collections::HashMap;

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct WallOpening {
    /// Distance from wall start along the wall centerline, in meters.
    pub from: f64,
    pub to: f64,
    /// Absolute Z elevations of opening sill and head, in meters.
    pub bottom: f64,
    pub top: f64,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum WallMeshError {
    NonFinite,
    InvalidWall,
    InvalidOpening,
    OverlappingOpenings,
    TooManyCells,
}

const MAX_CELLS: usize = 250_000;

/// Generate one closed, consistently oriented triangular surface for a wall
/// with rectangular through-openings. The centerline runs from start to end;
/// wall thickness is symmetric about that line. Openings may touch each other
/// at their edges, but may not overlap in area.
///
/// Quads are assembled independently, then shared corners are welded so the
/// resulting indexed mesh can be checked for watertight manifold topology.
/// Material and BIM element metadata are not yet encoded.
pub fn mesh_wall_with_openings(
    start: (f64, f64),
    end: (f64, f64),
    bottom_z: f64,
    top_z: f64,
    thickness: f64,
    openings: &[WallOpening],
) -> Result<TriangleMesh, WallMeshError> {
    let values = [start.0, start.1, end.0, end.1, bottom_z, top_z, thickness];
    if values.iter().any(|v| !v.is_finite()) {
        return Err(WallMeshError::NonFinite);
    }
    let dx = end.0 - start.0;
    let dy = end.1 - start.1;
    let length = dx.hypot(dy);
    if !length.is_finite()
        || length <= 0.0
        || !thickness.is_finite()
        || thickness <= 0.0
        || top_z <= bottom_z
    {
        return Err(WallMeshError::InvalidWall);
    }

    let mut us = vec![0.0, length];
    let mut zs = vec![bottom_z, top_z];
    for (index, opening) in openings.iter().enumerate() {
        let v = [opening.from, opening.to, opening.bottom, opening.top];
        if v.iter().any(|x| !x.is_finite()) {
            return Err(WallMeshError::NonFinite);
        }
        if opening.from < 0.0
            || opening.to > length
            || opening.from >= opening.to
            || opening.bottom < bottom_z
            || opening.top > top_z
            || opening.bottom >= opening.top
        {
            return Err(WallMeshError::InvalidOpening);
        }
        for previous in &openings[..index] {
            if opening.from < previous.to
                && previous.from < opening.to
                && opening.bottom < previous.top
                && previous.bottom < opening.top
            {
                return Err(WallMeshError::OverlappingOpenings);
            }
        }
        us.extend([opening.from, opening.to]);
        zs.extend([opening.bottom, opening.top]);
    }
    us.sort_by(f64::total_cmp);
    zs.sort_by(f64::total_cmp);
    us.dedup();
    zs.dedup();
    let cols = us.len() - 1;
    let rows = zs.len() - 1;
    if cols.checked_mul(rows).is_none_or(|count| count > MAX_CELLS) {
        return Err(WallMeshError::TooManyCells);
    }

    let mut filled = vec![true; cols * rows];
    for row in 0..rows {
        let z = zs[row] + (zs[row + 1] - zs[row]) / 2.0;
        for col in 0..cols {
            let u = us[col] + (us[col + 1] - us[col]) / 2.0;
            if openings
                .iter()
                .any(|o| u > o.from && u < o.to && z > o.bottom && z < o.top)
            {
                filled[row * cols + col] = false;
            }
        }
    }

    let tangent = (dx / length, dy / length);
    let outward = (tangent.1, -tangent.0);
    let point = |u: f64, z: f64, side: f64| -> Point3 {
        Point3 {
            x: start.0 + tangent.0 * u + outward.0 * side * thickness / 2.0,
            y: start.1 + tangent.1 * u + outward.1 * side * thickness / 2.0,
            z,
        }
    };
    let mut mesh = TriangleMesh::default();
    for row in 0..rows {
        let z0 = zs[row];
        let z1 = zs[row + 1];
        for col in 0..cols {
            if !filled[row * cols + col] {
                continue;
            }
            let u0 = us[col];
            let u1 = us[col + 1];
            // Front (+right) and back (-right) faces.
            append_quad(
                &mut mesh,
                [
                    point(u0, z0, 1.0),
                    point(u1, z0, 1.0),
                    point(u1, z1, 1.0),
                    point(u0, z1, 1.0),
                ],
            );
            append_quad(
                &mut mesh,
                [
                    point(u1, z0, -1.0),
                    point(u0, z0, -1.0),
                    point(u0, z1, -1.0),
                    point(u1, z1, -1.0),
                ],
            );
            // Left and right ends, plus opening jambs.
            if col == 0 || !filled[row * cols + col - 1] {
                append_quad(
                    &mut mesh,
                    [
                        point(u0, z0, -1.0),
                        point(u0, z0, 1.0),
                        point(u0, z1, 1.0),
                        point(u0, z1, -1.0),
                    ],
                );
            }
            if col + 1 == cols || !filled[row * cols + col + 1] {
                append_quad(
                    &mut mesh,
                    [
                        point(u1, z0, 1.0),
                        point(u1, z0, -1.0),
                        point(u1, z1, -1.0),
                        point(u1, z1, 1.0),
                    ],
                );
            }
            // Bottom and top horizontal surfaces, including sills and heads.
            if row == 0 || !filled[(row - 1) * cols + col] {
                append_quad(
                    &mut mesh,
                    [
                        point(u0, z0, 1.0),
                        point(u0, z0, -1.0),
                        point(u1, z0, -1.0),
                        point(u1, z0, 1.0),
                    ],
                );
            }
            if row + 1 == rows || !filled[(row + 1) * cols + col] {
                append_quad(
                    &mut mesh,
                    [
                        point(u1, z1, 1.0),
                        point(u1, z1, -1.0),
                        point(u0, z1, -1.0),
                        point(u0, z1, 1.0),
                    ],
                );
            }
        }
    }
    // Each quad initially owns its four corners. Reuse identical grid vertices
    // across adjacent quads to obtain a topologically connected indexed shell.
    // Normalizing negative zero avoids distinct keys for the same coordinate.
    let mut unique = HashMap::new();
    let mut positions = Vec::new();
    let mut remap = Vec::with_capacity(mesh.positions.len());
    for p in &mesh.positions {
        let bits = |v: f64| if v == 0.0 { 0 } else { v.to_bits() };
        let key = (bits(p.x), bits(p.y), bits(p.z));
        let index = *unique.entry(key).or_insert_with(|| {
            let index = positions.len();
            positions.push(*p);
            index
        });
        remap.push(index);
    }
    for triangle in &mut mesh.indices {
        for index in triangle {
            *index = remap[*index];
        }
    }
    mesh.positions = positions;
    Ok(mesh)
}

fn append_quad(mesh: &mut TriangleMesh, corners: [Point3; 4]) {
    let start = mesh.positions.len();
    mesh.positions.extend(corners);
    mesh.indices.push([start, start + 1, start + 2]);
    mesh.indices.push([start, start + 2, start + 3]);
}

#[cfg(test)]
mod tests {
    use super::*;

    fn area_opening() -> WallOpening {
        WallOpening {
            from: 1.0,
            to: 2.0,
            bottom: 1.0,
            top: 2.0,
        }
    }

    #[test]
    fn wall_without_openings_is_a_closed_box() {
        let mesh = mesh_wall_with_openings((0.0, 0.0), (4.0, 0.0), 0.0, 3.0, 0.2, &[]).unwrap();
        assert_eq!(mesh.triangle_count(), 12);
        assert!((mesh.signed_volume() - 2.4).abs() < 1e-9);
        assert!(mesh.is_closed_oriented_manifold());
        assert!((mesh.checked_closed_surface_area().unwrap() - 26.8).abs() < 1e-9);
    }

    #[test]
    fn opening_removes_exact_volume_and_creates_jambs() {
        let mesh =
            mesh_wall_with_openings((0.0, 0.0), (4.0, 0.0), 0.0, 3.0, 0.2, &[area_opening()])
                .unwrap();
        assert!((mesh.signed_volume() - 2.2).abs() < 1e-9);
        assert!(mesh.triangle_count() > 12);
        assert!(mesh.is_closed_oriented_manifold());
        assert!((mesh.checked_closed_surface_area().unwrap() - 25.6).abs() < 1e-9);
    }

    #[test]
    fn works_for_rotated_and_reversed_walls() {
        let cases = [
            ((0.0, 0.0), (0.0, 4.0)),
            ((4.0, 0.0), (0.0, 0.0)),
            ((2.0, 1.0), (2.0, 5.0)),
        ];
        for (a, b) in cases {
            let mesh = mesh_wall_with_openings(a, b, 0.0, 3.0, 0.2, &[area_opening()]).unwrap();
            assert!((mesh.signed_volume() - 2.2).abs() < 1e-9);
        }
    }

    #[test]
    fn rejects_invalid_and_overlapping_openings() {
        let open = area_opening();
        let overlap = WallOpening {
            from: 1.5,
            to: 2.5,
            bottom: 1.5,
            top: 2.5,
        };
        assert_eq!(
            mesh_wall_with_openings((0.0, 0.0), (4.0, 0.0), 0.0, 3.0, 0.2, &[open, overlap]),
            Err(WallMeshError::OverlappingOpenings)
        );
        let outside = WallOpening { from: -1.0, ..open };
        assert_eq!(
            mesh_wall_with_openings((0.0, 0.0), (4.0, 0.0), 0.0, 3.0, 0.2, &[outside]),
            Err(WallMeshError::InvalidOpening)
        );
        assert_eq!(
            mesh_wall_with_openings((0.0, 0.0), (0.0, 0.0), 0.0, 3.0, 0.2, &[]),
            Err(WallMeshError::InvalidWall)
        );
    }
}
