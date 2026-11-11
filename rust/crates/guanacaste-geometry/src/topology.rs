//! Preflight geometry checks for BIM footprints. No unsafe code.
#![forbid(unsafe_code)]

use crate::{GeometryError, Point2};

fn orient(a: Point2, b: Point2, c: Point2) -> f64 {
    (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)
}

fn on_segment(a: Point2, b: Point2, p: Point2) -> bool {
    orient(a, b, p) == 0.0
        && p.x >= a.x.min(b.x)
        && p.x <= a.x.max(b.x)
        && p.y >= a.y.min(b.y)
        && p.y <= a.y.max(b.y)
}

fn intersects(a: Point2, b: Point2, c: Point2, d: Point2) -> bool {
    let (ab_c, ab_d) = (orient(a, b, c), orient(a, b, d));
    let (cd_a, cd_b) = (orient(c, d, a), orient(c, d, b));
    ((ab_c > 0.0 && ab_d < 0.0) || (ab_c < 0.0 && ab_d > 0.0))
        && ((cd_a > 0.0 && cd_b < 0.0) || (cd_a < 0.0 && cd_b > 0.0))
        || (ab_c == 0.0 && on_segment(a, b, c))
        || (ab_d == 0.0 && on_segment(a, b, d))
        || (cd_a == 0.0 && on_segment(c, d, a))
        || (cd_b == 0.0 && on_segment(c, d, b))
}

/// Check that a polygon has finite vertices and non-intersecting edges.
///
/// Rejects duplicate vertices, coincident closing vertices, zero-length edges,
/// and collinear consecutive triples. Collinear triples are excluded from
/// extrusion because the caps may otherwise form T-junctions with side faces.
///
/// Uses exact f64 orientation predicates; near-collinear input should be
/// snapped/conditioned upstream before being accepted as a BIM footprint.
pub fn validate_extrusion_footprint(points: &[Point2]) -> Result<(), GeometryError> {
    let count = points.len();
    if count < 3 {
        return Err(GeometryError::TooFewVertices);
    }
    if points.iter().any(|p| !p.x.is_finite() || !p.y.is_finite()) {
        return Err(GeometryError::NonFiniteCoordinate);
    }
    for i in 0..count {
        let a = points[i];
        let b = points[(i + 1) % count];
        let c = points[(i + 2) % count];
        if a == b || orient(a, b, c) == 0.0 {
            return Err(GeometryError::DegeneratePolygon);
        }
    }
    for i in 0..count {
        let a = points[i];
        let b = points[(i + 1) % count];
        for j in (i + 1)..count {
            // Adjacent edges legitimately share a boundary vertex.
            if j == i + 1 || (i == 0 && j == count - 1) {
                continue;
            }
            if intersects(a, b, points[j], points[(j + 1) % count]) {
                return Err(GeometryError::NoEarFound);
            }
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn points(input: &[(f64, f64)]) -> Vec<Point2> {
        input
            .iter()
            .map(|&(x, y)| Point2::new(x, y).unwrap())
            .collect()
    }

    #[test]
    fn accepts_concave_outline_and_reversed_outline() {
        let mut l = points(&[
            (0.0, 0.0),
            (4.0, 0.0),
            (4.0, 1.0),
            (1.0, 1.0),
            (1.0, 3.0),
            (0.0, 3.0),
        ]);
        assert_eq!(validate_extrusion_footprint(&l), Ok(()));
        l.reverse();
        assert_eq!(validate_extrusion_footprint(&l), Ok(()));
    }

    #[test]
    fn rejects_crossing_edges_even_when_signed_area_nonzero() {
        let bow = points(&[(0.0, 0.0), (3.0, 3.0), (0.0, 4.0), (4.0, 0.0)]);
        assert_eq!(
            validate_extrusion_footprint(&bow),
            Err(GeometryError::NoEarFound)
        );
    }

    #[test]
    fn rejects_duplicate_closing_point_and_collinear_triples() {
        let repeated = points(&[(0.0, 0.0), (2.0, 0.0), (2.0, 1.0), (0.0, 1.0), (0.0, 0.0)]);
        assert_eq!(
            validate_extrusion_footprint(&repeated),
            Err(GeometryError::DegeneratePolygon)
        );
        let collinear = points(&[(0.0, 0.0), (1.0, 0.0), (2.0, 0.0), (2.0, 1.0), (0.0, 1.0)]);
        assert_eq!(
            validate_extrusion_footprint(&collinear),
            Err(GeometryError::DegeneratePolygon)
        );
    }

    #[test]
    fn rejects_touching_nonadjacent_edges() {
        let touching = points(&[(0.0, 0.0), (4.0, 0.0), (4.0, 3.0), (2.0, 0.0), (0.0, 3.0)]);
        assert_eq!(
            validate_extrusion_footprint(&touching),
            Err(GeometryError::NoEarFound)
        );
    }
}
