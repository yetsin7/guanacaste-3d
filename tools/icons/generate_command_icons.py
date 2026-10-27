"""Writes src/Guanacaste3D.Desktop/resources/icons.commands.xaml.

Every ribbon and quick-access command gets its own icon, "CmdIcon.<CommandId>", made of a pictogram from icon_kit.py
and, where it helps tell neighbours apart, a small badge. The ribbons look an icon up by command id first and fall back
to the older shared icon, so a command missing here still shows something sensible.

Run:  python tools/icons/generate_command_icons.py
"""

import os
import runpy
import sys

sys.path.insert(0, os.path.dirname(__file__))
from icon_kit import BADGES, BASES, FILLS, PENS  # noqa: E402

# command id -> (pictogram, badge or None)
MAP = {
    # Application and quick access
    "NewProject": ("new_file", None), "OpenProject": ("open_file", None), "Save": ("save", None), "SaveAs": ("save", "pencil"),
    "Undo": ("undo", None), "Redo": ("redo", None), "Model3D": ("cube", None), "RenderView": ("render", None),
    "ExportIfc": ("ifc", "arrow"), "ExportModel3D": ("cube", "arrow"), "CustomizeCommands": ("settings_more", "gear"),
    "Modify": ("modify", None), "Select": ("select", None),
    # Clipboard, properties, modify
    "TransmitModel": ("transmit", None), "Help": ("help", None), "About": ("about", None), "FamilyConverter": ("family_converter", None),
    "AiAssistant": ("ai", None), "Properties": ("properties", None), "Paste": ("paste", None), "Cut": ("cut", None), "Copy": ("copy", None),
    "MatchProperties": ("match", None), "TrimExtend": ("trim", None), "CutGeometry": ("cut_by", None), "JoinGeometry": ("join", None),
    "UnjoinGeometry": ("unjoin", None), "Move": ("move", None), "CopyElement": ("copy", "plus"), "CreateSimilar": ("create_similar", None),
    "Rotate": ("rotate", None), "MirrorPickAxis": ("mirror", None), "Align": ("align", None), "Array": ("array", None), "Scale": ("scale", None),
    "Split": ("split", None), "Offset": ("offset", None), "Pin": ("pin", None), "Delete": ("delete", None), "Measure": ("measure", None),
    "MeasureAlong": ("measure_along", None), "CreateGroup": ("group", "plus"), "CreateAssembly": ("assembly", "plus"),
    # Wall tools
    "WallProperties": ("wall", "gear"), "WallTypes": ("wall", "tag"), "Levels": ("level", "gear"), "OpeningTypes": ("window_el", "gear"),
    "AttachTopBase": ("attach", None), "DetachTopBase": ("detach", None), "AttachTop": ("attach", "up"), "AttachBase": ("attach", "down"),
    "DetachAll": ("detach", "x"), "EditProfile": ("profile", "pencil"), "ResetProfile": ("profile", "rotate"), "FlipWall": ("flip", None),
    "WallOpenings": ("opening_wall", None), "WallJoins": ("joins", None), "AllowJoin": ("joins", "check"), "DisallowJoin": ("joins", "x"),
    "MiterJoin": ("miter_join", None), "ButtJoin": ("butt_join", None), "SquareOff": ("square_off", None),
    # Views
    "Plan": ("view_plan", None), "Elevation": ("view_elevation", None), "Section": ("view_section", None),
    # Structure
    "Beam": ("beam", None), "StructColumn": ("column", None), "Truss": ("truss", None), "BeamSystem": ("beam_system", None), "Brace": ("brace", None),
    "Connection": ("bolts", None), "ConnectionAuto": ("bolts", "gear"), "IsolatedFoundation": ("foundation", None), "WallFoundation": ("foundation_wall", None),
    "FoundationSlab": ("foundation_slab", None), "WallByFace": ("wall_face", None), "Floor": ("floor", None),
    # Architecture
    "Component": ("family", "plus"), "ModelText": ("text_model", None), "ModelLine": ("model_line", None), "ModelGroup": ("model_group", None),
    "OpeningByFace": ("opening_face", None), "ShaftOpening": ("opening_shaft", None), "WallOpening": ("opening_wall", None),
    "VerticalOpening": ("opening_vertical", None), "DormerOpening": ("opening_dormer", None), "FaceOpening": ("opening_face", None),
    "Level": ("level", None), "Grid": ("grid", None), "SetWorkPlane": ("work_plane", "gear"), "ShowWorkPlane": ("work_plane", "eye"),
    "Viewer": ("viewer", None), "ReferencePlane": ("ref_plane", None),
    "Wall": ("wall", None), "Door": ("door", None), "Window": ("window", None), "Column": ("column", None), "Roof": ("roof", None),
    "Ceiling": ("ceiling", None), "CurtainSystem": ("curtain", "plus"), "CurtainGrid": ("curtain", None), "Mullion": ("mullion", None),
    "Railing": ("railing", None), "Ramp": ("ramp", None), "Stair": ("stair", None), "Room": ("room", None), "RoomSeparator": ("separator", None),
    "TagRoom": ("room", "tag"), "Area": ("area", None), "AreaBoundary": ("area_boundary", None), "TagArea": ("area", "tag"),
    "RoofByFace": ("roof_extrusion", None), "FloorByFace": ("floor", "arrow"), "WallArchitectural": ("wall", None), "WallStructural": ("wall_corner", None),
    "WallSweep": ("wall_sweep", None), "WallReveal": ("wall_sweep", "minus"), "ColumnArchitectural": ("column_round", None), "ColumnStructural": ("column", None),
    "ComponentPlace": ("family", "plus"), "ComponentInPlace": ("family", "pencil"), "RoofByFootprint": ("roof", None), "RoofByExtrusion": ("roof_extrusion", None),
    "RoofSoffit": ("roof_soffit", None), "RoofFascia": ("roof_fascia", None), "RoofGutter": ("gutter", None), "CeilingAutomatic": ("ceiling", "gear"),
    "CeilingSketch": ("ceiling", "pencil"), "FloorArchitectural": ("floor", None), "FloorStructural": ("floor", "gear"), "SlabEdge": ("slab_edge", None),
    "RailingSketch": ("railing", "pencil"), "RailingOnHost": ("railing", "arrow"), "GroupCreate": ("group", "plus"), "GroupPlace": ("group", "arrow"),
    "RoomAutomatic": ("room_auto", None), "TagAllRooms": ("tag_all", None), "AreaByBoundary": ("area_boundary", "arrow"), "TagAllAreas": ("tag_all", None),
    "WorkPlaneByLevel": ("work_plane", "arrow"), "WorkPlanePickReference": ("work_plane", "pencil"), "Deselect": ("select", "x"),
    "Gutter": ("gutter", None), "Downspout": ("downspout", None), "Skylight": ("window_el", "up"), "ApplyWallType": ("wall", "check"),
    # Concrete
    "Rebar": ("rebar", None), "RebarPath": ("rebar_path", None), "RebarArea": ("rebar_area", None), "TransformedFabric": ("fabric_transformed", None),
    "FabricArea": ("fabric", "plus"), "FabricSheet": ("fabric", None), "Cover": ("cover", None), "CoverSettings": ("cover", "gear"),
    "ReinforcementSettings": ("rebar", "gear"), "AssemblyParts": ("assembly", None), "PrecastReinforcement": ("precast", "plus"),
    "CustomFabricSheet": ("fabric", "pencil"), "ShopDrawings": ("shop_drawings", None), "CamExport": ("cam_export", None),
    "PrecastConfiguration": ("precast", "gear"), "CesConfiguration": ("configuration", None),
    # Steel
    "Plate": ("plate", None), "Bolts": ("bolts", None), "Welds": ("welds", None), "CornerCut": ("corner_cut", None), "ParametricCope": ("cope", "gear"),
    "Shorten": ("shorten", None), "ContourCut": ("cut_by", "pencil"), "Cope": ("cope", None), "Miter": ("miter", None), "CutThrough": ("cut_through", None),
    "CutBy": ("cut_by", None),
    # Systems
    "Duct": ("duct", None), "DuctPlaceholder": ("duct_placeholder", None), "FlexDuct": ("flex_duct", None), "AirTerminal": ("air_terminal", None),
    "FabricationPart": ("fabrication", None), "MultipointRouting": ("routing", None), "DuctStiffener": ("stiffener", None), "PidModeler": ("pid", None),
    "MechanicalEquipment": ("mechanical_equipment", None), "MechanicalControlDevice": ("control_device", None), "Pipe": ("pipe", None),
    "FlexPipe": ("flex_pipe", None), "PlumbingFixture": ("plumbing_fixture", None), "Wire": ("wire", None), "CableTray": ("cable_tray", None),
    "Conduit": ("conduit", None), "ElectricalEquipment": ("panel_board", None),
    "LightingFixture": ("luminaire", None), "ElectricalDevice": ("outlet", None), "LightSwitch": ("light_switch", None),
    "PanelSchedule": ("schedule", "star"),
    # Insert
    "ManageLinks": ("link", "gear"), "LinkNativeProject": ("link", "plus"), "LinkIfc": ("ifc", "link"), "LinkCad": ("link_cad", None),
    "LinkTopography": ("link_topo", None), "DwfMarkups": ("markup", None), "PointCloud": ("point_cloud", None), "CoordinationModel": ("coordination", None),
    "ImportCad": ("cad", "down"), "ImportGbxml": ("energy", "down"), "ImportPdf": ("pdf", "down"), "ImportImage": ("image", "down"),
    "ExtendedProperties": ("properties", "plus"), "Decal": ("decal", None), "ArchitecturalLibrary": ("library", None), "LoadFamily": ("family", "down"),
    "LoadCloudFamily": ("cloud", "down"), "LoadGroup": ("group", "down"), "InsertFromFile": ("file_insert", None),
    # Annotate
    "AlignedDimension": ("dimension_aligned", None), "LinearDimension": ("dimension", None), "AngularDimension": ("dimension_angular", None),
    "RadialDimension": ("dimension_radial", None), "DiameterDimension": ("dimension_diameter", None), "ArcLengthDimension": ("dimension_arc", None),
    "ElevationDimension": ("spot_elevation", None), "SpotCoordinate": ("spot_coordinate", None), "SpotSlope": ("spot_slope", None),
    "DetailLine": ("detail_line", None), "FilledRegion": ("filled_region", None), "DetailComponent": ("detail_component", None),
    "RevisionCloud": ("revision_cloud", None), "DetailGroup": ("group", "pencil"), "Insulation": ("insulation", None), "Text": ("text", None),
    "Spelling": ("text", "check"), "FindReplace": ("text", "arrow"), "TagByCategory": ("tag", None), "TagAll": ("tag_all", None), "Keynote": ("keynote", None),
    "AreaTag": ("area", "tag"), "MaterialTag": ("materials", "tag"), "ColorFillLegend": ("legend", None), "Symbol": ("symbol", None),
    # Analyze
    "AnalyticalMember": ("beam", "gear"), "AnalyticalPanel": ("floor", "gear"), "AnalyticalLink": ("link", "gear"), "AnalyticalBoundary": ("area_boundary", "gear"),
    "AnalyticalOpening": ("opening_wall", "gear"), "Space": ("space", None), "SpaceSeparator": ("separator", None), "SpaceTag": ("space", "tag"),
    "Zone": ("zone", None), "Reports": ("reports", None), "ModelCheck": ("model_check", None), "StructuralAnalysis": ("structural_analysis", None),
    "SystemsAnalysis": ("systems_analysis", None), "EnergyOptimization": ("energy", None), "FormCreation": ("form_creation", None),
    "SolarAnalysis": ("sun", "gear"), "ResultsFill": ("results_fill", None),
    # Mass and site
    "ShowMass": ("mass", "eye"), "InPlaceMass": ("mass_inplace", "pencil"), "PlaceMass": ("mass", "plus"), "Sketch3D": ("model_line", "pencil"),
    "Toposolid": ("toposolid", None), "ContextualData": ("topography", "down"), "SiteComponent": ("site_component", None), "ParkingComponent": ("parking", None),
    "PropertyLine": ("property_line", None), "LabelContours": ("contours", None), "GradedRegion": ("graded_region", None),
    # Collaborate
    "EditRequests": ("edit_requests", None), "Collaborate": ("collaborate", None), "Worksets": ("worksets", None), "SynchronizeCentral": ("sync", None),
    "ReloadLatest": ("reload", None), "Relinquish": ("relinquish", None), "WorksharingMonitor": ("monitor", None), "ShowHistory": ("history", None),
    "RestoreBackup": ("backup", "arrow"), "PublishSettings": ("publish", "gear"), "ManageCloudModels": ("cloud", "gear"), "Coordinate": ("coordinate", None),
    "Share": ("share", None),
    # View
    "ViewTemplates": ("view_templates", None), "VisibilityGraphics": ("visibility", None), "Filters": ("filter", None), "ThinLines": ("thin_lines", None),
    "ShowHiddenLines": ("hidden_lines", "eye"), "RemoveHiddenLines": ("hidden_lines", "x"), "CutProfile": ("cut_profile", None),
    "Render": ("render", None), "CloudRender": ("render_cloud", None), "RealtimeVisualization": ("realtime", None), "Camera": ("camera", None),
    "Walkthrough": ("walkthrough", None), "Callout": ("callout", None), "Schedule": ("schedule", None), "Orbit": ("orbit", None), "Pan": ("pan", None),
    "Zoom": ("zoom", None), "ViewCube": ("view_cube", None), "Perspective": ("perspective", None), "Orthographic": ("orthographic", None),
    "SectionBox": ("section_box", None), "VisualStyle": ("visual_style", None), "Shadows": ("shadows", None), "SunPath": ("sun_path", None),
    "LockView": ("lock_view", None), "ResetCamera": ("reset_camera", None), "Sheet": ("sheet", None), "Matchline": ("matchline", None),
    "GuideGrid": ("guide_grid", None), "ViewReference": ("view_reference", None), "SwitchWindows": ("windows_switch", None),
    "CloseHiddenWindows": ("windows_close", None), "TabViews": ("windows_tab", None), "TileViews": ("windows_tile", None),
    "UserInterface": ("ui", None), "CanvasTheme": ("theme", None),
    # Manage
    "Materials": ("materials", None), "ProjectParameters": ("object_styles", None), "SharedParameters": ("shared_parameters", None), "Numbering": ("numbering", None),
    "ProjectUnits": ("units", None), "AdditionalSettings": ("settings_more", None), "Location": ("location", None), "Coordinates": ("coordinates", None),
    "Position": ("position", None), "DesignOptions": ("design_options", None), "CreateStudy": ("design_options", "plus"), "ExploreOutcomes": ("analysis", "eye"),
    "Phases": ("phases", None), "SelectById": ("select_id", None), "ReviewWarnings": ("warning", None), "Automation": ("automation", None),
    "AutomationPlayer": ("player", None), "Schedules": ("schedule", "gear"),
}

HEAD = """<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <!--
    One icon per command ("CmdIcon.<CommandId>"), generated by tools/icons/generate_command_icons.py from the
    pictograms in tools/icons/icon_kit.py. Do not edit by hand: change the generator and run it again.
    Ribbons and the quick access bar look an icon up by command id first, then fall back to the older shared icons.
  -->
  <Pen x:Key="CmdGreenPen" Brush="#2E8B57" Thickness="1.5" StartLineCap="Round" EndLineCap="Round" LineJoin="Round"/>
  <Pen x:Key="CmdOrangePen" Brush="#C26A00" Thickness="1.5" StartLineCap="Round" EndLineCap="Round" LineJoin="Round"/>
  <Pen x:Key="CmdBadgeRing" Brush="#263746" Thickness="1" StartLineCap="Round" EndLineCap="Round" LineJoin="Round"/>
  <Pen x:Key="CmdBadgeGreen" Brush="#2E8B57" Thickness="1.4" StartLineCap="Round" EndLineCap="Round" LineJoin="Round"/>
  <Pen x:Key="CmdBadgeRed" Brush="#C83F49" Thickness="1.4" StartLineCap="Round" EndLineCap="Round" LineJoin="Round"/>
  <Pen x:Key="CmdBadgeAccent" Brush="#007C9F" Thickness="1.4" StartLineCap="Round" EndLineCap="Round" LineJoin="Round"/>
  <Pen x:Key="CmdBadgeOrange" Brush="#C26A00" Thickness="1.4" StartLineCap="Round" EndLineCap="Round" LineJoin="Round"/>
  <Pen x:Key="CmdBadgeInk" Brush="#263746" Thickness="1.3" StartLineCap="Round" EndLineCap="Round" LineJoin="Round"/>
  <Pen x:Key="CadOutlinePen" Brush="#808080" Thickness="0.9" StartLineCap="Square" EndLineCap="Square" LineJoin="Miter"/>
  <Pen x:Key="CadAccentPen" Brush="#3298D3" Thickness="1.05" StartLineCap="Square" EndLineCap="Square" LineJoin="Miter"/>
  <Pen x:Key="CadDangerPen" Brush="#E53935" Thickness="2.4" StartLineCap="Square" EndLineCap="Square" LineJoin="Miter"/>
"""

BADGE_PENS = {"G": "CmdBadgeGreen", "R": "CmdBadgeRed", "A": "CmdBadgeAccent", "O": "CmdBadgeOrange", "I": "CmdBadgeInk"}


def drawing(geometry, fill, pen):
    """Serializes a vector primitive using the shared CAD palette and pen resources."""
    attributes = []
    if fill:
        attributes.append(f'Brush="{FILLS[fill]}"')
    if pen:
        attributes.append(f'Pen="{{StaticResource {PENS[pen]}}}"')
    return f'    <GeometryDrawing {" ".join(attributes)} Geometry="{geometry}"/>'


def icon_xml(name, base_name, badge_name, prefix="CmdIcon"):
    """Emits a fixed-grid icon so compact and large buttons preserve identical visual scale."""
    lines = [f'  <DrawingImage x:Key="{prefix}.{name}"><DrawingImage.Drawing><DrawingGroup>',
             '    <GeometryDrawing Brush="Transparent" Geometry="M0,0 H24 V24 H0 Z"/>']
    for geometry, fill, pen in BASES[base_name]:
        lines.append(drawing(geometry, fill, pen))
    if badge_name:
        cx, cy = 18.4, 18.4
        lines.append(f'    <GeometryDrawing Brush="#FFFFFF" Pen="{{StaticResource CmdBadgeRing}}" Geometry="M{cx - 4.6},{cy} A4.6,4.6 0 1 0 {cx + 4.6},{cy} A4.6,4.6 0 1 0 {cx - 4.6},{cy} Z"/>')
        geometry, pen = BADGES[badge_name](cx, cy)
        lines.append(f'    <GeometryDrawing Pen="{{StaticResource {BADGE_PENS[pen]}}}" Geometry="{geometry}"/>')
    lines.append("  </DrawingGroup></DrawingImage.Drawing></DrawingImage>")
    return "\n".join(lines)


def main():
    """Regenerates command icons and legacy aliases from the same reference-driven artwork."""
    theme = runpy.run_path(os.path.join(os.path.dirname(__file__), "cad-icon-theme.py"))
    FILLS.update(theme["PALETTE"])
    PENS.update(theme["PEN_NAMES"])
    BASES.update(theme["SHAPES"])
    target = os.path.join(os.path.dirname(__file__), "..", "..", "src", "Guanacaste3D.Desktop", "resources", "icons.commands.xaml")
    missing = [pair for pair in MAP.values() if pair[0] not in BASES]
    if missing:
        raise SystemExit(f"unknown pictograms: {sorted({m[0] for m in missing})}")
    body = [HEAD]
    for name in sorted(MAP):
        base_name, badge_name = MAP[name]
        body.append(icon_xml(name, base_name, badge_name))
    for name, command_id in sorted(theme["SHARED_COMMANDS"].items()):
        base_name, badge_name = MAP[command_id]
        body.append(icon_xml(name, base_name, badge_name, "Icon"))
    body.append("</ResourceDictionary>\n")
    with open(target, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(body))
    print(f"{len(MAP)} icons from {len(BASES)} pictograms -> {os.path.normpath(target)}")


if __name__ == "__main__":
    main()
