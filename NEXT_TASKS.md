# Next tasks

Ordered by impact. Each task ends with a check in `tests/Guanacaste3D.Checks`
and, for UI work, a UI Automation screenshot.

1. **Validate IFC4 export with IfcOpenShell** (and open it in Blender/Bonsai or
   FreeCAD). The export is done and passes structural checks.
2. **IFC import** for the same entities (extruded-area solids), the practical
   way to bring Revit models in.
3. **Roofs and ceilings**: flat and pitched roofs from a boundary, with slope
   per edge; ceilings per room.
4. **Render materials**: material per wall type and floor, plus sky turbidity
   and an exposure control in the Render tab. Extend the denoiser through glass
   (features of the first non-glass hit).
5. **Columns and beams** with structural types and quantity takeoff.
6. **Wall joins** (L/T cleanup) in plan and 3D.
7. **PDF output** for sheets.
9. **SKP import** via the official SketchUp C SDK (P/Invoke), once the owner
   downloads the SDK.
10. Fix the title-bar overlap (KNOWN_ISSUES #4) and localize core-created names.
