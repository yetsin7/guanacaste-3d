# Neufert research and model coverage

## Reference

The publisher identifies *Architects' Data*, sixth English edition, Wiley-Blackwell,
2023, ISBN 978-1-119-87394-5, as an architectural planning reference. Its public
catalog lists dimensioned examples and building-type guidance, and reports updated
sections on stairs, lifts, lighting and fire protection. The table of contents lists
accessibility, dimensional basics, building components, residential rooms and many
other occupancies. Source: [Wiley publisher catalog](https://www.wiley.com/en-us/architects%27-data-6th-edition-p-9781119873945).

The public catalog establishes the book's identity and scope, but does not expose
the technical content necessary to validate individual numerical requirements.
This document therefore does not treat the table of contents as a source of
dimensions or claim that the application follows the book. The relevant edition,
page, context, measurement definition and conditions must be checked against an
accessible copy before a rule is attributed to it. Local codes and the authority
having jurisdiction need a separate assessment.

## Current model observations

These observations describe source code inspected in the current working tree;
they are not results of an interactive application test.

| Area | Existing representation | Gap before a meaningful design review |
| --- | --- | --- |
| Walls, doors and windows | Wall geometry, hosted openings and a dimensioned type catalog. | The opening dimensions alone do not establish clear passage, functional use, accessibility, adjoining clearances or contextual requirements. |
| Rooms | Rectangular annotations with a name, two corners and computed area. | The record does not explicitly classify occupancy or describe a general room boundary, so area and clearance checks cannot be generalized reliably. |
| Stairs | Straight, open, L-shaped, U-shaped and spiral representations. The stair geometry derives riser counts from a fixed maximum. | The modeled run can vary independently, and the individual flight dimensions need to be evaluated using the actual generated geometry. A spiral needs a specified walking line. Headroom and contextual usage also need independent modeling and review. |
| Ramps and guards | Structural elements include ramps and railings. | A preset label or element width alone is not evidence of accessible slope, landing geometry, clear width or handrail compliance. |
| Residential components | A catalog describes component shapes and overall dimensions. | Furniture dimensions alone do not measure the free space required to use an element, pass it or open associated equipment. |
| General audit | `ModelAudit` reports selected geometric coordination findings. | Its existing checks do not cover Neufert design guidance. |

Relevant implementation entry points are `bim-elements.cs`, `WallOpenings.cs`,
`model-audit.cs`, `architecture/stair-geometry.cs`, `architecture/element-geometry.cs`,
`architecture/architectural-library.cs`, and
`architecture/residential-component-catalog.cs` under `src/Guanacaste3D.Core`.

## Implementation requirements

1. Establish a traceable, reviewed rule catalog with an edition, page or figure,
   applicability, metric measurement definition and classification as a design
   recommendation or an independently sourced regulatory requirement.
2. Record the building use and other context needed to select applicable rules;
   report missing information as unknown rather than passing the check.
3. Measure actual modeled geometry and functional clearances, including per-flight
   stair geometry, openings and obstructions. Distinguish a catalog dimension from
   clear space available in the project.
4. Make findings identifiable by element and level, explain the measured value,
   target and source, and rerun them after edits. Preserve user projects and avoid
   silently changing existing model geometry.
5. Test boundaries, inapplicable and unknown cases, rotated geometry, model edits,
   save and reopen, and desktop presentation. Treat a passing subset as a limited
   review, not as proof that the whole building satisfies the book or local code.

No Neufert rule catalog or application behavior is implemented by this research
note. Full coverage requires the relevant technical content and an architectural
review of each rule and its applicability.
