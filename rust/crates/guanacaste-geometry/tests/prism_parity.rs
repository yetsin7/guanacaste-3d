//! Cross-check the Rust prism mesh against the analytical rectangular-prism
//! volume used by the legacy C# geometry tests. This is not a C# execution.
use guanacaste_geometry::mesh::extrude_footprint;
use guanacaste_geometry::Point2;

#[test]
fn rectangular_prisms_match_legacy_analytic_volume_at_survey_offsets() {
    for (width, depth, height) in [(3.0, 2.0, 4.0), (0.5, 0.8, 2.5), (12.0, 7.0, 0.25)] {
        let footprint = [
            Point2::new(0.0, 0.0).unwrap(),
            Point2::new(width, 0.0).unwrap(),
            Point2::new(width, depth).unwrap(),
            Point2::new(0.0, depth).unwrap(),
        ];
        let expected = width * depth * height;
        let baseline = extrude_footprint(&footprint, 0.0, height).unwrap();
        let mut translated = baseline.clone();
        for vertex in &mut translated.positions {
            vertex.x += 1_000_000.0;
            vertex.y -= 1_000_000.0;
            vertex.z += 1_000_000.0;
        }
        for mesh in [&baseline, &translated] {
            let actual = mesh.checked_closed_volume().unwrap();
            assert!((actual - expected).abs() < 1e-8, "expected {expected}, got {actual}");
        }
    }
}
