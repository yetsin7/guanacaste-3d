//! Renderer-neutral, metric Z-up indexed triangle meshes.
//! Extrusion currently supports simple, hole-free polygons; its faces are
//! oriented outward and its cap triangulation reuses the C#-compatible ear clipper.
#![forbid(unsafe_code)]

use crate::{triangulate, GeometryError, Point2};
use std::collections::HashMap;

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Point3 {
    pub x: f64,
    pub y: f64,
    pub z: f64,
}

impl Point3 {
    pub fn new(x: f64, y: f64, z: f64) -> Result<Self, GeometryError> {
        if !x.is_finite() || !y.is_finite() || !z.is_finite() {
            return Err(GeometryError::NonFiniteCoordinate);
        }
        Ok(Self { x, y, z })
    }
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct TriangleMesh {
    pub positions: Vec<Point3>,
    pub indices: Vec<[usize; 3]>,
}

impl TriangleMesh {
    pub fn triangle_count(&self) -> usize {
        self.indices.len()
    }

    pub fn bounds(&self) -> Option<(Point3, Point3)> {
        let first = *self.positions.first()?;
        if !first.x.is_finite() || !first.y.is_finite() || !first.z.is_finite() {
            return None;
        }
        let mut min = first;
        let mut max = first;
        for point in &self.positions[1..] {
            if !point.x.is_finite() || !point.y.is_finite() || !point.z.is_finite() {
                return None;
            }
            min.x = min.x.min(point.x);
            min.y = min.y.min(point.y);
            min.z = min.z.min(point.z);
            max.x = max.x.max(point.x);
            max.y = max.y.max(point.y);
            max.z = max.z.max(point.z);
        }
        Some((min, max))
    }

    /// Checks that every mesh edge belongs to exactly two oppositely oriented
    /// triangles. Rejects invalid indices, non-finite coordinates and degenerate
    /// triangles; this is a topological check, not a self-intersection test.
    pub fn is_closed_oriented_manifold(&self) -> bool {
        if self.indices.is_empty() || self.positions.is_empty() || self.bounds().is_none() {
            return false;
        }
        let mut edges: HashMap<(usize, usize), (u32, i32)> = HashMap::new();
        for &[a, b, c] in &self.indices {
            if a >= self.positions.len() || b >= self.positions.len() || c >= self.positions.len()
                || a == b || b == c || c == a
            {
                return false;
            }
            let p = self.positions[a];
            let q = self.positions[b];
            let r = self.positions[c];
            let ux = q.x - p.x;
            let uy = q.y - p.y;
            let uz = q.z - p.z;
            let vx = r.x - p.x;
            let vy = r.y - p.y;
            let vz = r.z - p.z;
            let cross = [uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx];
            if cross.iter().any(|v| !v.is_finite()) || cross.iter().all(|v| *v == 0.0) {
                return false;
            }
            for (from, to) in [(a, b), (b, c), (c, a)] {
                let key = (from.min(to), from.max(to));
                let sign = if from < to { 1 } else { -1 };
                let entry = edges.entry(key).or_insert((0, 0));
                entry.0 += 1;
                entry.1 += sign;
                if entry.0 > 2 { return false; }
            }
        }
        edges.values().all(|&(count, orientation)| count == 2 && orientation == 0)
    }

    /// Signed volume of an oriented closed triangular surface.
    /// Returns NaN for invalid indices or non-finite coordinates rather than panicking.
    /// Meaningful only for consistently oriented, watertight meshes.
    pub fn signed_volume(&self) -> f64 {
        self.checked_signed_volume().unwrap_or(f64::NAN)
    }

    /// Checked volume for meshes received from untrusted importers or FFI callers.
    /// This validates indices and finite coordinates, not watertight topology.
    pub fn checked_signed_volume(&self) -> Option<f64> {
        let mut volume = 0.0;
        for &[a, b, c] in &self.indices {
            let a = self.positions.get(a)?;
            let b = self.positions.get(b)?;
            let c = self.positions.get(c)?;
            if [a, b, c]
                .iter()
                .any(|p| !p.x.is_finite() || !p.y.is_finite() || !p.z.is_finite())
            {
                return None;
            }
            volume += (a.x * (b.y * c.z - b.z * c.y)
                + a.y * (b.z * c.x - b.x * c.z)
                + a.z * (b.x * c.y - b.y * c.x))
                / 6.0;
        }
        volume.is_finite().then_some(volume)
    }
}

/// Build a closed prism of a simple footprint polygon between two elevations.
/// Indices reference the original input points duplicated for bottom and top.
/// Polygon holes are not supported; self-intersections, repeated coordinates,
/// and collinear consecutive edges are rejected before mesh generation.
pub fn extrude_footprint(
    footprint: &[Point2],
    bottom_z: f64,
    top_z: f64,
) -> Result<TriangleMesh, GeometryError> {
    if !bottom_z.is_finite() || !top_z.is_finite() {
        return Err(GeometryError::NonFiniteCoordinate);
    }
    if top_z <= bottom_z {
        return Err(GeometryError::DegeneratePolygon);
    }
    crate::topology::validate_extrusion_footprint(footprint)?;
    let caps = triangulate(footprint)?;
    let count = footprint.len();
    let mut positions = Vec::with_capacity(count * 2);
    for &point in footprint {
        positions.push(Point3::new(point.x, point.y, bottom_z)?);
    }
    for &point in footprint {
        positions.push(Point3::new(point.x, point.y, top_z)?);
    }
    let mut indices = Vec::with_capacity(caps.len() * 2 + count * 2);
    for [a, b, c] in caps {
        indices.push([c, b, a]);
        indices.push([a + count, b + count, c + count]);
    }
    // Side quads must follow CCW boundary order irrespective of input winding.
    let ccw = crate::signed_double_area(footprint) > 0.0;
    for i in 0..count {
        let j = (i + 1) % count;
        let (a, b) = if ccw { (i, j) } else { (j, i) };
        indices.push([a, b, b + count]);
        indices.push([a, b + count, a + count]);
    }
    Ok(TriangleMesh { positions, indices })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::signed_double_area;

    fn rectangle() -> Vec<Point2> {
        vec![
            Point2::new(0.0, 0.0).unwrap(),
            Point2::new(3.0, 0.0).unwrap(),
            Point2::new(3.0, 2.0).unwrap(),
            Point2::new(0.0, 2.0).unwrap(),
        ]
    }

    #[test]
    fn rectangular_prism_has_expected_volume_bounds_and_triangles() {
        let mesh = extrude_footprint(&rectangle(), 1.0, 5.0).unwrap();
        assert_eq!(mesh.positions.len(), 8);
        assert_eq!(mesh.triangle_count(), 12);
        let (min, max) = mesh.bounds().unwrap();
        assert_eq!(min, Point3::new(0.0, 0.0, 1.0).unwrap());
        assert_eq!(max, Point3::new(3.0, 2.0, 5.0).unwrap());
        assert!((mesh.signed_volume() - 24.0).abs() < 1e-10);
    }

    #[test]
    fn reversed_winding_keeps_positive_volume() {
        let mut outline = rectangle();
        outline.reverse();
        assert!(signed_double_area(&outline) < 0.0);
        let mesh = extrude_footprint(&outline, 0.0, 2.0).unwrap();
        assert!((mesh.signed_volume() - 12.0).abs() < 1e-10);
    }

    #[test]
    fn concave_footprint_produces_correct_volume() {
        let polygon = [
            (0.0, 0.0),
            (4.0, 0.0),
            (4.0, 1.0),
            (1.0, 1.0),
            (1.0, 3.0),
            (0.0, 3.0),
        ]
        .map(|(x, y)| Point2::new(x, y).unwrap());
        let mesh = extrude_footprint(&polygon, -1.0, 2.0).unwrap();
        assert_eq!(mesh.triangle_count(), 20);
        assert!((mesh.signed_volume() - 18.0).abs() < 1e-10);
    }

    #[test]
    fn closed_manifold_rejects_open_flipped_and_malformed_meshes() {
        let mut mesh = extrude_footprint(&rectangle(), 0.0, 2.0).unwrap();
        assert!(mesh.is_closed_oriented_manifold());
        mesh.indices.pop();
        assert!(!mesh.is_closed_oriented_manifold());
        let mut mesh = extrude_footprint(&rectangle(), 0.0, 2.0).unwrap();
        mesh.indices[0].swap(0, 1);
        assert!(!mesh.is_closed_oriented_manifold());
        let mut mesh = extrude_footprint(&rectangle(), 0.0, 2.0).unwrap();
        mesh.indices[0][0] = mesh.positions.len();
        assert!(!mesh.is_closed_oriented_manifold());
        let mut mesh = extrude_footprint(&rectangle(), 0.0, 2.0).unwrap();
        mesh.positions[0].x = f64::NAN;
        assert!(!mesh.is_closed_oriented_manifold());
    }

    #[test]
    fn malformed_mesh_volume_is_checked_without_panicking() {
        let mut mesh = extrude_footprint(&rectangle(), 0.0, 2.0).unwrap();
        assert_eq!(mesh.checked_signed_volume(), Some(12.0));
        mesh.indices[0][0] = mesh.positions.len();
        assert_eq!(mesh.checked_signed_volume(), None);
        assert!(mesh.signed_volume().is_nan());
        mesh.indices[0][0] = 0;
        mesh.positions[0].x = f64::NAN;
        assert_eq!(mesh.checked_signed_volume(), None);
        assert!(mesh.signed_volume().is_nan());
    }

    #[test]
    fn rejects_invalid_elevations_and_empty_mesh_bounds() {
        assert_eq!(
            extrude_footprint(&rectangle(), 2.0, 2.0),
            Err(GeometryError::DegeneratePolygon)
        );
        assert_eq!(
            extrude_footprint(&rectangle(), f64::NAN, 2.0),
            Err(GeometryError::NonFiniteCoordinate)
        );
        assert_eq!(TriangleMesh::default().bounds(), None);
        let mut mesh = extrude_footprint(&rectangle(), 0.0, 2.0).unwrap();
        mesh.positions[0].x = f64::NAN;
        assert_eq!(mesh.bounds(), None);
        mesh.positions[0].x = 0.0;
        mesh.positions[1].z = f64::INFINITY;
        assert_eq!(mesh.bounds(), None);
    }
}
