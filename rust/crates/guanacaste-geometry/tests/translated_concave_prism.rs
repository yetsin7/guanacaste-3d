//! Analytic regression for translated concave footprints; not an executed C# fixture.
use guanacaste_geometry::mesh::extrude_footprint;
use guanacaste_geometry::Point2;

#[test]
fn translated_concave_prism_preserves_metric_area_and_volume() {
    // L footprint: area 6 m², perimeter 14 m, height 3 m.
    // Local coordinates reduce cancellation in volume integration.
    for (dx, dy) in [(0.0, 0.0), (1_000_000.0, -1_000_000.0)] {
        let mut footprint = [
            (0.0, 0.0),
            (4.0, 0.0),
            (4.0, 1.0),
            (1.0, 1.0),
            (1.0, 3.0),
            (0.0, 3.0),
        ]
        .into_iter()
        .map(|(x, y)| Point2::new(x + dx, y + dy).unwrap())
        .collect::<Vec<_>>();

        for _ in 0..2 {
            let mesh = extrude_footprint(&footprint, -1.0, 2.0).unwrap();
            assert!(mesh.is_closed_oriented_manifold());
            assert!((mesh.checked_closed_volume().unwrap() - 18.0).abs() < 1e-8);
            assert!((mesh.checked_closed_surface_area().unwrap() - 54.0).abs() < 1e-8);
            footprint.reverse();
        }
    }
}
