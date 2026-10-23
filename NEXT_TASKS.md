# Next tasks

Ordered by impact. Each task ends with a check in `tests/Guanacaste3D.Checks`
and, for UI work, a UI Automation screenshot.

1. **IFC4 export** (walls, slabs, openings, storeys, spaces) written as STEP
   text with no dependencies, then validated with IfcOpenShell when available.
   This is the bridge to Revit, ArchiCAD, Blender/Bonsai and FreeCAD.
2. **IFC import** for the same entities (extruded-area solids), the practical
   way to bring Revit models in.
3. **Roofs and ceilings**: flat and pitched roofs from a boundary, with slope
   per edge; ceilings per room.
4. **Render materials**: material per wall type and floor, plus sky turbidity
   and an exposure control in the Render tab; denoising (à-trous filter guided
   by normals and albedo) so low sample counts look clean.
5. **Columns and beams** with structural types and quantity takeoff.
6. **Wall joins** (L/T cleanup) in plan and 3D.
7. **PDF output** for sheets.
8. **OBJ/glTF export** of the shared mesh, so models open in Blender/Lumion.
9. **SKP import** via the official SketchUp C SDK (P/Invoke), once the owner
   downloads the SDK.
10. Fix the title-bar overlap (KNOWN_ISSUES #4) and localize core-created names.
