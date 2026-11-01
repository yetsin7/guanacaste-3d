# Guanacaste 3D exporter for Revit 1.0.1

Extract the entire ZIP and run `install.cmd`. The package includes the versions
listed in its RevitYYYY folders; this release ships Revit 2027 only.
Restart Revit after installation. The add-in is installed for the current user;
Windows may request administrator credentials if the bundled .NET 10 Desktop
runtime is not already installed.

The Guanacaste 3D ribbon exports the whole model or the active 3D view to `.g3dx`.
New files contain a 512-pixel preview. The included Windows thumbnail component
also previews older `.g3dx`, `.g3d`, `.g3dt`, and `.g3df` files. Refresh Explorer
with F5 after installing. The desktop application is needed to open/edit models;
it is not needed to display their thumbnails.

Close Revit and run `uninstall.cmd` to remove the exporter. Shared file thumbnails
remain available to Guanacaste 3D. Remove them separately with
`powershell -File register-thumbnails.ps1 -Unregister` if no application needs them.

The exporter preserves editable walls, hosted openings and supported slabs;
other geometry becomes mesh families when imported. This is not full Revit parity.
The optional preview uses exported geometry, not a screenshot of the Revit window.
