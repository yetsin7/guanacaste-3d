---
version: alpha
name: Guanacaste 3D
description: A dense Windows architectural workspace with reference-driven CAD iconography.
colors:
  primary: "#3298D3"
  outline: "#808080"
  surface: "#ECECEC"
  shade: "#B8B8B8"
  danger: "#E53935"
  success: "#25A85A"
  ribbon: "#F5F5F5"
typography:
  sans:
    fontFamily: Segoe UI
omitted:
  - section: spacing
    reason: Existing WPF theme metrics remain authoritative.
  - section: rounded
    reason: Existing WPF control templates own corner radii.
components:
  ribbon:
    backgroundColor: "{colors.ribbon}"
  command-icon:
    backgroundColor: "{colors.surface}"
  command-icon-side:
    backgroundColor: "{colors.shade}"
  command-icon-delete:
    textColor: "{colors.danger}"
  command-icon-complete:
    textColor: "{colors.success}"
---

# Guanacaste 3D Design System

## Overview

The creative reference is the user's supplied CAD ribbon screenshots: recognizable architectural
silhouettes, neutral shaded solids, fine gray outlines, blue editing marks, and compact command groups.
The audience creates coordinated building models, plans, sheets, and rendered presentations on Windows.
Spanish is the current user interface locale; identifiers and developer documentation use English.
The product register prioritizes command recognition and stable desktop density. Existing branding
remains the Guanacaste 3D identity. Decorative badges and heavy colored outlines are anti-references.

Runtime icon tokens are owned by `tools/icons/cad-icon-theme.py` and the pens emitted by
`tools/icons/generate_command_icons.py`. `resources/icons.commands.xaml` is generated output.
The existing `resources/theme.xaml` owns typography, control metrics, surfaces, focus, and interaction states.

## Colors

Neutral surfaces and shade describe modeled solids. Blue describes glass, active model faces, and
editing marks. Red marks deletion; green marks completion. Color supplements geometry and labels.
The palette in the frontmatter mirrors the runtime icon generator and WPF ribbon surface.

## Typography

Segoe UI follows the existing Windows workspace typography. Ribbon labels remain localized text
beside or beneath icons; technical values follow the active culture. Developer evidence uses English.

## Layout

All command artwork uses a 24 by 24 coordinate grid with fixed transparent bounds. Ribbons retain
the existing shared large and compact button metrics. At compact widths, the shared ribbon viewport
must keep the final group reachable. Icons do not change control dimensions on hover or selection.

## Elevation & Depth

Model icons use pale front faces and darker side faces to communicate volume. Shadows are reserved
for existing overlays and dialogs. The ribbon itself stays flat and quiet.

## Shapes

Fine square-ended gray strokes preserve the drafting character. Physical objects use recognizable
wall, door, column, slab, and stair silhouettes. Editing marks retain their conventional arrows and axes.

## Components

Command resources are resolved by `CommandIcons.For` in ribbons, menus, and quick access controls.
Legacy `Icon.*` aliases are generated from the same artwork to keep browser and property icons aligned.
WPF button styles own hover, pressed, keyboard focus, and disabled states. Icon-only controls retain
localized accessible names. Material and walkthrough commands reveal the existing Render controls.
Motion follows existing explicit tool demonstrations; hover does not start an animation.

## Do's and Don'ts

- Regenerate icon resources after changing artwork or palette.
- Keep command labels, icons, and real behavior aligned.
- Validate all tab resources and compact ribbon access in the real WPF window.
- Do not replace a functioning workflow with a decorative control or a static preview.
