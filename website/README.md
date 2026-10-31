# Guanacaste 3D website

This folder contains a self-contained, dependency-free promotional landing page in `index.html`.

## Preview locally

Open `website/index.html` in a modern browser. No package installation or server is required.

## Publish

This commit intentionally does **not** replace an existing live site or configure DNS. If the current website lives in another repository or hosting provider, integrate the design there after reviewing its routing, analytics, domain, and deployment pipeline. For GitHub Pages, deploy the `website/` directory via an approved Pages workflow or copy the verified output to the configured publishing source.

The mock BIM interface is an **illustration**, not an actual product screenshot. Replace it with permission-cleared screenshots from real builds before promotional distribution.

All download buttons point to the GitHub releases index instead of an unpublished asset. Check that a release is actually **published** before running campaigns.

## Pre-launch checklist

- Publish and test a signed Windows build if feasible, or transparently explain SmartScreen warnings.
- Provide SHA-256 checksums and plain-English release notes.
- Test fresh Windows 10 and 11 installation, zip extraction, startup, and sample model workflow.
- Add authentic product screenshots, a short captioned demonstration video, and performance measurements that can be replicated.
- Complete accessibility, keyboard, mobile and SEO checks on the final hosting environment.
- Enable basic privacy-conscious measurement of page visits → release-page clicks → completed downloads.
- Ensure public bug-report links work and the community is supported.

No external tracking, libraries, or fonts are loaded by this page.
