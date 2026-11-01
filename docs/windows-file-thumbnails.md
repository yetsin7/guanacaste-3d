# Windows file thumbnails

The Windows x64 package includes a per-user thumbnail provider for `.g3d`, `.g3dt`, `.g3df`, and `.g3dx`.
It works with existing files without saving them again, modifying their container format, or creating sidecar images.
The standard Windows thumbnail cache handles reuse and refresh after a file changes.

## Content

- Projects: fixed isometric feature-edge view of model geometry, excluding the ground plane.
- Mesh families: their embedded preview mesh, or the full mesh when no preview exists.
- Extrusion families: regenerated geometry of the first type.
- Sheet families: the stored paper border and line/rectangle graphics; text is omitted at thumbnail size.
- Legacy opening families: a dimensioned outline of the first type with an inset frame.
- Families without tessellatable solids: a schematic operation tree rather than invented model geometry.
- Templates and empty projects: stored level elevations, since templates have no building geometry.
- Revit/SketchUp exchange files: embedded PNG when present, otherwise a shaded view of the exported meshes, including instance translations and rotations.

The Revit 1.0.1 exporter embeds a 512-pixel PNG without changing the version-1 geometry payload. Its installer also
registers Windows thumbnails, including for older exchange files. Complete mesh surfaces are retained up to a
500,000-triangle preview budget; larger exchanges prioritize the building envelope and may omit detailed components.
The shaded preview uses triangle depth sorting and is not a production renderer.

This is an Explorer thumbnail integration, not a Windows Preview Pane handler or a photorealistic rendering service.
No external family paths, textures, or network references are opened.

## Build and registration

`scripts/install-windows.ps1` publishes and registers the provider automatically.
The Desktop `PublishThumbnailProvider` target bundles it under `thumbnail-handler` for win-x64 publications.
The provider requires the same .NET 10 Desktop runtime as the framework-dependent application.
Downloadable desktop and Revit installer ZIPs include Microsoft's offline Desktop runtime installer, verified
against the SHA-512 published in its release metadata. It runs only if the x64 .NET 10 Desktop runtime is missing.
The desktop installer is built with `scripts/package-windows-installer.ps1`; users run `install.cmd` after extraction.

To install just the thumbnail component without closing the application:

```powershell
dotnet publish src/windows-thumbnails/windows-thumbnails.csproj -c Release --self-contained false -o artifacts/thumbnail-provider
./scripts/register-thumbnails.ps1 -ProviderDirectory ./artifacts/thumbnail-provider
```

Registration uses HKCU, including extension, application ProgID, and SystemFileAssociations entries.
Content-addressed install directories avoid overwriting loaded DLLs during updates.
Windows receives an association-change notification; Explorer is not restarted and visibility preferences are preserved.
Older installed provider directories are retained because a Shell client may still have them loaded.

```powershell
./scripts/register-thumbnails.ps1 -Unregister
```

Unregistration removes this provider's associations and COM registration without changing the file-opening command.

## Hosting and limits

The provider implements `IInitializeWithStream` and `IThumbnailProvider` through a .NET COM host.
The per-user registration sets `DisableProcessIsolation=1`: the restricted Windows thumbnail surrogate returned
`REGDB_E_CLASSNOTREG` for per-user activation on the validation machine. Consequently the managed provider runs
in the requesting Shell process, with WPF drawing on a dedicated STA thread. It does not have crash isolation
from that process. Moving to a native broker or a machine-wide isolated deployment is future work.

Native containers retain SHA-256 verification and identity checks. Input is capped at the existing 256 MiB container
limit (20 MiB for legacy text files), decompression stops at the declared payload length, and output is capped at
1024 pixels per edge. Rendering requires parsing and meshing the document on the first uncached request; very large
models can therefore take longer and consume substantial memory. Shell calls now wait at most 15 seconds
for the dedicated STA renderer; the caller receives a timeout failure instead of waiting indefinitely.
The HBITMAP is allocated only after a successful wait, so a late renderer cannot leak a GDI bitmap.
Only one thumbnail render is admitted at a time per Shell process. If the caller times out, its STA worker
keeps the render slot until it actually exits; overlapping requests fail with `ERROR_BUSY` rather than
starting more expensive geometry jobs. This is a partial resource-exhaustion mitigation, not a cancellation mechanism.
This is **not** a hard CPU or memory limit: the background worker may continue after a timeout because
the provider still runs in the requesting process. The isolation risk remains open in issue #23.
Corrupt or unsupported files fail extraction so Windows can retain the ordinary file icon. The provider never changes thumbnail visibility settings.

## Verification

```powershell
dotnet run --project tests/thumbnail-checks/thumbnail-checks.csproj -c Release -- artifacts/thumbnail-checks
dotnet run --project tests/thumbnail-checks/thumbnail-checks.csproj -c Release -- artifacts/thumbnail-shell-checks --shell
```

The second command requires registration. It requests `IShellItemImageFactory` thumbnails with `SIIGBF_THUMBNAILONLY`,
so an ordinary application icon cannot make the check pass. Fixtures cover all three extensions, both container
generations, mesh and extrusion families, paper families, and non-geometric families. Checks exercise four sizes,
nonblank opaque output, unchanged source bytes, corrupted checksums, truncated headers, forged decompression lengths,
repeated requests, caller ownership of GDI bitmaps, and Windows cache refresh after saved geometry changes.
PNGs remain alongside the generated fixture files for review.
Exchange checks cover old files, embedded previews, invalid embedded-image fallback, unchanged BIM fields,
the existing importer, and optional `--exchange-file=<absolute-path>` read-only checks on a local file.

References: [Windows thumbnail handlers](https://learn.microsoft.com/en-us/windows/win32/shell/thumbnail-providers),
[IThumbnailProvider](https://learn.microsoft.com/en-us/windows/win32/api/thumbcache/nn-thumbcache-ithumbnailprovider),
and [.NET COM hosting](https://learn.microsoft.com/en-us/dotnet/core/native-interop/expose-components-to-com).
