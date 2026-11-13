//! Analytical metric surface-area regression for the Rust mesh implementation.
//! These cases do not execute the legacy C# geometry engine.
use guanacaste_geometry::mesh::extrude_footprint;
use guanacaste_geometry::Point2;

#[test]
fn rectangular_prism_surface_area_is_metric_and_translation_invariant() {
    for (width, depth, height) in [(3.0, 2.0, 4.0), (0.5, 0.8, 2.5)] {
        let footprint = [
            Point2::new(0.0, 0.0).unwrap(),
            Point2::new(width, 0.0).unwrap(),
            Point2::new(width, depth).unwrap(),
            Point2::new(0.0, depth).unwrap(),
        ];
        let expected = 2.0 * (width * depth + width * height + depth * height);
        let mesh = extrude_footprint(&footprint, 0.0, height).unwrap();
        assert!((mesh.checked_closed_surface_area().unwrap() - expected).abs() < 1e-10);
        let mut translated = mesh.clone();
        for vertex in &mut translated.positions {
            vertex.x += 1_000_000.0;
            vertex.y -= 1_000_000.0;
            vertex.z += 1_000_000.0;
        }
        assert!((translated.checked_closed_surface_area().unwrap() - expected).abs() < 1e-8);
        let mut reversed = mesh.clone();
        for triangle in &mut reversed.indices {
            triangle.swap(1, 2);
        }
        assert!((reversed.checked_closed_surface_area().unwrap() - expected).abs() < 1e-10);
    }
}

#[test]
fn open_mesh_does_not_report_closed_surface_area() {
    let footprint = [
        Point2::new(0.0, 0.0).unwrap(),
        Point2::new(3.0, 0.0).unwrap(),
        Point2::new(3.0, 2.0).unwrap(),
        Point2::new(0.0, 2.0).unwrap(),
    ];
    let mut mesh = extrude_footprint(&footprint, 0.0, 4.0).unwrap();
    mesh.indices.pop();
    assert_eq!(mesh.checked_closed_surface_area(), None);
}
