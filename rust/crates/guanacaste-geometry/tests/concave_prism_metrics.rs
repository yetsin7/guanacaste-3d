//! Analytical reference for a concave extrusion; not an executed C# parity fixture.
use guanacaste_geometry::mesh::extrude_footprint;
use guanacaste_geometry::Point2;

#[test]
fn concave_prism_area_volume_and_winding_match_analytic_reference() {
    // L-shaped footprint: 4*1 + 1*2 = 6 m²; perimeter = 14 m.
    // Extrusion from -1 to 2 m: volume 18 m³, area 2*6 + 14*3 = 54 m².
    let mut footprint = vec![
        (0.0, 0.0),
        (4.0, 0.0),
        (4.0, 1.0),
        (1.0, 1.0),
        (1.0, 3.0),
        (0.0, 3.0),
    ]
    .into_iter()
    .map(|(x, y)| Point2::new(x, y).unwrap())
    .collect::<Vec<_>>();

    for _ in 0..2 {
        let mesh = extrude_footprint(&footprint, -1.0, 2.0).unwrap();
        assert!(mesh.is_closed_oriented_manifold());
        assert!((mesh.checked_closed_volume().unwrap() - 18.0).abs() < 1e-10);
        assert!((mesh.checked_closed_surface_area().unwrap() - 54.0).abs() < 1e-10);
        footprint.reverse();
    }
}
