//! Regression tests for closed-surface area over disconnected solid shells.
//! Analytical checks only; executable C# parity remains pending.
use guanacaste_geometry::mesh::{extrude_footprint, TriangleMesh};
use guanacaste_geometry::Point2;

fn prism(width: f64, depth: f64, height: f64) -> TriangleMesh {
    let footprint = [
        Point2::new(0.0, 0.0).unwrap(),
        Point2::new(width, 0.0).unwrap(),
        Point2::new(width, depth).unwrap(),
        Point2::new(0.0, depth).unwrap(),
    ];
    extrude_footprint(&footprint, 0.0, height).unwrap()
}

#[test]
fn disjoint_closed_shells_have_additive_area() {
    let mut first = prism(3.0, 2.0, 4.0);
    let mut second = prism(1.0, 2.0, 3.0);
    for p in &mut second.positions {
        p.x += 100.0;
    }
    let offset = first.positions.len();
    first.positions.extend(second.positions);
    for [a, b, c] in second.indices {
        first.indices.push([a + offset, b + offset, c + offset]);
    }
    assert!(first.is_closed_oriented_manifold());
    assert!((first.checked_closed_surface_area().unwrap() - 74.0).abs() < 1e-10);
}

#[test]
fn duplicated_face_rejects_closed_surface_area() {
    let mut mesh = prism(3.0, 2.0, 4.0);
    mesh.indices.push(mesh.indices[0]);
    assert!(!mesh.is_closed_oriented_manifold());
    assert_eq!(mesh.checked_closed_surface_area(), None);
}

#[test]
fn translated_and_reversed_shell_preserves_surface_area() {
    let mut mesh = prism(3.0, 2.0, 4.0);
    let expected = 2.0 * (3.0 * 2.0 + 3.0 * 4.0 + 2.0 * 4.0);
    for point in &mut mesh.positions {
        point.x += 1_000_000.0;
        point.y -= 1_000_000.0;
        point.z += 1_000_000.0;
    }
    for triangle in &mut mesh.indices {
        triangle.swap(1, 2);
    }
    assert!(mesh.is_closed_oriented_manifold());
    assert!((mesh.checked_closed_surface_area().unwrap() - expected).abs() < 1e-9);
    assert!((mesh.checked_closed_volume().unwrap() + 24.0).abs() < 1e-9);
}
