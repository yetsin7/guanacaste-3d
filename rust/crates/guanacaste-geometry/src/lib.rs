//! Port of Guanacaste3D.Core.Geometry.PolygonTriangulator.
//! Indices refer to input vertices; triangle orientation is counterclockwise.
//! Metric coordinates, double precision; no unsafe code.
#![forbid(unsafe_code)]

/// A finite, two-dimensional project point in meters.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Point2 {
    pub x: f64,
    pub y: f64,
}

impl Point2 {
    pub fn new(x: f64, y: f64) -> Result<Self, GeometryError> {
        if !(x.is_finite() && y.is_finite()) {
            return Err(GeometryError::NonFiniteCoordinate);
        }
        Ok(Self { x, y })
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum GeometryError {
    TooFewVertices,
    NonFiniteCoordinate,
    DegeneratePolygon,
    NoEarFound,
}

/// Translate the shoelace products to the first point to prevent cancellation
/// for buildings with large geographic coordinate offsets.
pub fn signed_double_area(polygon: &[Point2]) -> f64 {
    let Some(origin) = polygon.first() else {
        return 0.0;
    };
    polygon[1..]
        .windows(2)
        .map(|edge| {
            let a = edge[0];
            let b = edge[1];
            (a.x - origin.x) * (b.y - origin.y)
                - (b.x - origin.x) * (a.y - origin.y)
        })
        .sum()
}

fn cross(a: Point2, b: Point2, c: Point2) -> f64 {
    (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x)
}

fn contains_other_vertex(
    polygon: &[Point2],
    remaining: &[usize],
    a: usize,
    b: usize,
    c: usize,
) -> bool {
    for &index in remaining {
        if index == a || index == b || index == c {
            continue;
        }
        let p = polygon[index];
        if p == polygon[a] || p == polygon[b] || p == polygon[c] {
            continue;
        }
        if cross(polygon[a], polygon[b], p) >= 0.0
            && cross(polygon[b], polygon[c], p) >= 0.0
            && cross(polygon[c], polygon[a], p) >= 0.0
        {
            return true;
        }
    }
    false
}

/// Ear-clip a simple polygon into CCW triangles, using input vertex indices.
/// Behavior and thresholds intentionally match the current C# implementation.
/// Self-intersecting polygons are unsupported; not every such polygon can be
/// detected by ear clipping, so input topology must be validated separately.
pub fn triangulate(polygon: &[Point2]) -> Result<Vec<[usize; 3]>, GeometryError> {
    if polygon.len() < 3 {
        return Err(GeometryError::TooFewVertices);
    }
    if polygon
        .iter()
        .any(|point| !point.x.is_finite() || !point.y.is_finite())
    {
        return Err(GeometryError::NonFiniteCoordinate);
    }
    let area = signed_double_area(polygon);
    if !area.is_finite() || area.abs() < 1e-12 {
        return Err(GeometryError::DegeneratePolygon);
    }

    let mut remaining: Vec<usize> = (0..polygon.len()).collect();
    if area < 0.0 {
        remaining.reverse();
    }
    let mut triangles = Vec::with_capacity(polygon.len() - 2);
    let mut guard = 0usize;
    while remaining.len() > 3 {
        let mut clipped = false;
        for i in 0..remaining.len() {
            let previous = remaining[(i + remaining.len() - 1) % remaining.len()];
            let current = remaining[i];
            let next = remaining[(i + 1) % remaining.len()];
            let turn = cross(polygon[previous], polygon[current], polygon[next]);
            if turn.abs() < 1e-14 {
                remaining.remove(i);
                clipped = true;
                break;
            }
            if turn < 0.0 || contains_other_vertex(polygon, &remaining, previous, current, next) {
                continue;
            }
            triangles.push([previous, current, next]);
            remaining.remove(i);
            clipped = true;
            break;
        }
        guard += 1;
        if !clipped || guard > polygon.len().saturating_mul(polygon.len()) {
            return Err(GeometryError::NoEarFound);
        }
    }
    if cross(
        polygon[remaining[0]],
        polygon[remaining[1]],
        polygon[remaining[2]],
    )
    .abs()
        > 1e-14
    {
        triangles.push([remaining[0], remaining[1], remaining[2]]);
    }
    Ok(triangles)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn poly(points: &[(f64, f64)]) -> Vec<Point2> {
        points
            .iter()
            .map(|&(x, y)| Point2::new(x, y).unwrap())
            .collect()
    }

    fn summed_triangle_area(points: &[Point2], triangles: &[[usize; 3]]) -> f64 {
        triangles
            .iter()
            .map(|&[a, b, c]| cross(points[a], points[b], points[c]) / 2.0)
            .sum()
    }

    #[test]
    fn rejects_invalid_inputs() {
        assert_eq!(triangulate(&[]), Err(GeometryError::TooFewVertices));
        assert_eq!(
            Point2::new(f64::NAN, 0.0),
            Err(GeometryError::NonFiniteCoordinate)
        );
        assert_eq!(
            triangulate(&poly(&[(0.0, 0.0), (1.0, 0.0), (2.0, 0.0)])),
            Err(GeometryError::DegeneratePolygon)
        );
    }

    #[test]
    fn convex_and_reversed_orientation_are_equivalent() {
        let square = poly(&[(0.0, 0.0), (2.0, 0.0), (2.0, 2.0), (0.0, 2.0)]);
        let forward = triangulate(&square).unwrap();
        assert_eq!(forward.len(), 2);
        assert_eq!(summed_triangle_area(&square, &forward), 4.0);
        let reversed: Vec<Point2> = square.into_iter().rev().collect();
        let backward = triangulate(&reversed).unwrap();
        assert_eq!(backward.len(), 2);
        assert_eq!(summed_triangle_area(&reversed, &backward), 4.0);
    }

    #[test]
    fn triangulates_concave_l_shape() {
        let points = poly(&[
            (0.0, 0.0),
            (4.0, 0.0),
            (4.0, 1.0),
            (1.0, 1.0),
            (1.0, 3.0),
            (0.0, 3.0),
        ]);
        let triangles = triangulate(&points).unwrap();
        assert_eq!(triangles.len(), 4);
        assert_eq!(summed_triangle_area(&points, &triangles), 6.0);
        assert!(triangles
            .iter()
            .all(|&[a, b, c]| cross(points[a], points[b], points[c]) > 0.0));
    }

    #[test]
    fn large_coordinate_offset_preserves_small_areas() {
        let square = poly(&[
            (1e8, 1e8),
            (1e8 + 0.25, 1e8),
            (1e8 + 0.25, 1e8 + 0.25),
            (1e8, 1e8 + 0.25),
        ]);
        assert_eq!(signed_double_area(&square), 0.125);
        let triangles = triangulate(&square).unwrap();
        assert_eq!(summed_triangle_area(&square, &triangles), 0.0625);
    }

    #[test]
    fn removable_collinear_vertex_preserves_area() {
        let points = poly(&[
            (0.0, 0.0),
            (1.0, 0.0),
            (2.0, 0.0),
            (2.0, 2.0),
            (0.0, 2.0),
        ]);
        let triangles = triangulate(&points).unwrap();
        assert_eq!(summed_triangle_area(&points, &triangles), 4.0);
    }
}
