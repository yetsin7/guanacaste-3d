//! Renderer-neutral, metric Z-up indexed triangle meshes.
//! Extrusion currently supports simple, hole-free polygons; its faces are
//! oriented outward and its cap triangulation reuses the C#-compatible ear clipper.
#![forbid(unsafe_code)]

use crate::{triangulate, GeometryError, Point2};

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
        let mut min = first;
        let mut max = first;
        for point in &self.positions[1..] {
            min.x = min.x.min(point.x);
            min.y = min.y.min(point.y);
            min.z = min.z.min(point.z);
            max.x = max.x.max(point.x);
            max.y = max.y.max(point.y);
            max.z = max.z.max(point.z);
        }
        Some((min, max))
    }

    /// Signed volume of an oriented closed triangular surface.
    /// Meaningful only for consistently oriented, watertight meshes.
    pub fn signed_volume(&self) -> f64 {
        self.indices
            .iter()
            .map(|&[a, b, c]| {
                let a = self.positions[a];
                let b = self.positions[b];
                let c = self.positions[c];
                (a.x * (b.y * c.z - b.z * c.y)
                    + a.y * (b.z * c.x - b.x * c.z)
                    + a.z * (b.x * c.y - b.y * c.x))
                    / 6.0
            })
            .sum()
    }
}

/// Build a closed prism of a simple footprint polygon between two elevations.
/// Indices reference the original input points duplicated for bottom and top.
/// Polygon holes, self-intersections and duplicated consecutive vertices are
/// not supported; callers must ensure a simple, non-degenerate outline.
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
    }
}
