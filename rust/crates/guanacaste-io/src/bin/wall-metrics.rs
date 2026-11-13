//! Read-only metrics inspection for the supported schema-15 wall subset.
//! Usage: cargo run -p guanacaste-io --bin wall-metrics -- <path>
use guanacaste_io::{
    bim_slice::{extract_wall_surface_areas, extract_wall_volumes},
    inspect_legacy_json, inspect_native, inspect_preview, MAX_NATIVE_BYTES,
};
use std::{env, fs, io::Read, process};

fn run(path: &str) -> Result<(), String> {
    // Open first, then inspect that exact handle: a path may be replaced between
    // separate stat/open calls (including through a symlink).
    let file = fs::File::open(path).map_err(|err| format!("cannot read input: {err}"))?;
    let metadata = file.metadata().map_err(|err| format!("cannot stat input: {err}"))?;
    if metadata.len() > MAX_NATIVE_BYTES as u64 {
        return Err("input exceeds native document limit".into());
    }
    // Bound the read itself: a concurrent writer cannot force unbounded allocation.
    let mut bytes = Vec::new();
    file.take(MAX_NATIVE_BYTES as u64 + 1)
        .read_to_end(&mut bytes)
        .map_err(|err| format!("cannot read input: {err}"))?;
    // Metadata can become stale if another process grows the file during the read.
    if bytes.len() > MAX_NATIVE_BYTES {
        return Err("input exceeds native document limit".into());
    }
    let inspected = if bytes.starts_with(b"G3DNATV1") {
        inspect_native(&bytes)
    } else if bytes.starts_with(b"G3-PROJECT/1
") {
        inspect_preview(&bytes)
    } else {
        inspect_legacy_json(&bytes)
    }
    .map_err(|err| format!("invalid document envelope: {err:?}"))?;
    let volumes = extract_wall_volumes(&inspected)
        .map_err(|err| format!("unsupported wall subset: {err:?}"))?;
    let areas = extract_wall_surface_areas(&inspected)
        .map_err(|err| format!("unsupported wall subset: {err:?}"))?;
    if volumes.len() != areas.len() {
        return Err("wall metric counts do not match".into());
    }
    // Validate every pair before emitting any output: stdout is an atomic report.
    if volumes
        .iter()
        .zip(&areas)
        .any(|(volume, area)| volume.wall_id != area.wall_id)
    {
        return Err("wall metric IDs do not match".into());
    }
    for (volume, area) in volumes.iter().zip(&areas) {
        println!(
            "{:032x}, {:.12}, {:.12}",
            volume.wall_id, volume.signed_volume_m3, area.surface_area_m2
        );
    }
    Ok(())
}

fn main() {
    let mut args = env::args().skip(1);
    let Some(path) = args.next() else {
        eprintln!("usage: wall-metrics <project-file>");
        process::exit(2);
    };
    if args.next().is_some() {
        eprintln!("usage: wall-metrics <project-file>");
        process::exit(2);
    }
    if let Err(err) = run(&path) {
        eprintln!("{err}");
        process::exit(1);
    }
}
