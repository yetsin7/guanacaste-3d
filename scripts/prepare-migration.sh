#!/usr/bin/env bash
set -euo pipefail
# Run on a developer workstation with git, Node.js 22+, npm, and authenticated gh.
# This script never deletes source folders or changes source repositories.
ROOT="${1:-$PWD/guanacaste-3d}"
if [[ -e "$ROOT" ]]; then echo "Destination already exists: $ROOT" >&2; exit 1; fi
command -v gh >/dev/null || { echo "GitHub CLI (gh) required" >&2; exit 1; }
gh auth status >/dev/null
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
gh repo clone yetsin7/mo-design-3d "$TMP/mo"
gh repo clone yetsin7/mau-design "$TMP/mau"
gh repo clone yetsin7/guanacaste-3d "$ROOT"
mkdir -p "$ROOT/legacy-reference/mau-design-render"
cp -a "$TMP/mo/." "$ROOT/"
rm -rf "$ROOT/.git"
# Restore destination repository Git history, never combine or overwrite origin history.
git -C "$TMP/mo" rev-parse HEAD > "$ROOT/legacy-reference/mo-source-commit.txt"
git -C "$TMP/mau" rev-parse HEAD > "$ROOT/legacy-reference/mau-source-commit.txt"
for file in render-export.ts render-lighting.ts render-pipeline.ts render-viewport.tsx; do
  cp "$TMP/mau/frontend/src/presentation/$file" "$ROOT/legacy-reference/mau-design-render/$file"
done
# Use target git metadata freshly; gh repo clone checked this repository out already.
git -C "$ROOT" init -q
git -C "$ROOT" branch -M main
git -C "$ROOT" remote remove origin 2>/dev/null || true
git -C "$ROOT" remote add origin https://github.com/yetsin7/guanacaste-3d.git
node - "$ROOT" <<'NODE'
const fs = require('node:fs');
const path = require('node:path');
const root = process.argv[2];
const read = name => fs.readFileSync(path.join(root,name),'utf8');
const write = (name,value) => fs.writeFileSync(path.join(root,name),value);
let pkg = JSON.parse(read('package.json'));
pkg.name='guanacaste-3d'; pkg.version='1.0.0'; pkg.author='Guanacaste 3D';
pkg.description='Local architectural 2D/3D modeling, design, documentation and rendering';
pkg.scripts.dev='npm run dev -w guanacaste-3d-frontend';
pkg.scripts.preview='npm run preview -w guanacaste-3d-frontend';
pkg.scripts.build='npm run typecheck && npm run build -w guanacaste-3d-frontend && npm run build:revit-bridge';
pkg.build.appId='design.guanacaste.3d'; pkg.build.productName='Guanacaste 3D';
pkg.build.artifactName='Guanacaste-3D-${version}-${arch}.${ext}';
pkg.build.nsis.shortcutName='Guanacaste 3D';
pkg.build.nsis.artifactName='Guanacaste-3D-${version}-${arch}-Setup.${ext}';
write('package.json', JSON.stringify(pkg,null,2)+'\n');
let front=JSON.parse(read('frontend/package.json'));
front.name='guanacaste-3d-frontend'; front.version='1.0.0';
write('frontend/package.json',JSON.stringify(front,null,2)+'\n');
let lock=JSON.parse(read('package-lock.json'));
lock.name=pkg.name;lock.version=pkg.version;
if(lock.packages?.['']){lock.packages[''].name=pkg.name;lock.packages[''].version=pkg.version;}
if(lock.packages?.frontend){lock.packages.frontend.name=front.name;lock.packages.frontend.version=front.version;}
write('package-lock.json',JSON.stringify(lock,null,2)+'\n');
const replace=(name, from, to)=>write(name,read(name).replaceAll(from,to));
replace('frontend/index.html','Mo Design 3D','Guanacaste 3D');
for(const lang of ['es','en']){
 const filename='frontend/src/i18n/'+lang+'.json';
 let strings=JSON.parse(read(filename)); strings['app.name']='Guanacaste 3D';
 write(filename,JSON.stringify(strings,null,2)+'\n');
}
const main='desktop/main.cjs';
let m=read(main).replaceAll('Mo Design 3D','Guanacaste 3D')
  .replaceAll("design.mo.3d","design.guanacaste.3d")
  .replaceAll("'mo-design-3d'","'guanacaste-3d'")
  .replaceAll('MO_DESIGN_3D_DEV_URL','GUANACASTE_3D_DEV_URL')
  .replaceAll('mo-design-3d:close-requested','guanacaste-3d:close-requested')
  .replaceAll('mo-design-3d:resolve-close','guanacaste-3d:resolve-close');
write(main,m);
// Leave other legacy IPC names in place until they are reviewed together.
// This prevents breaking IPC by changing only one side.
NODE
# Preserve the original operational documentation.
cp "$TMP/mo/README.md" "$ROOT/legacy-reference/mo-design-readme.md"
cp "$TMP/mau/README.md" "$ROOT/legacy-reference/mau-design-readme.md"
cat > "$ROOT/README.md" <<'README'
# Guanacaste 3D
Arquitectura, documentación, modelado 2D/3D, renderizado físico y presupuestos.

## Origen y seguridad
Base del producto: `yetsin7/mo-design-3d` (v1.0.2). Revisión adicional:
`yetsin7/mau-design` (v0.5.0). Sus repositorios y datos existentes
no se modifican. Las cuatro implementaciones antiguas de render están en
`legacy-reference/mau-design-render/` para evaluación, NO dentro del build.

## Desarrollo
Node.js 22.12+, `npm ci`, `npm run typecheck`, `npm test`,
`npm run build`, `npm run test:e2e`, `npm start`.
Los empaquetados Windows y la migración de datos requieren validación específica.

## Política de migración
NO borrar instalaciones ni carpetas de Mau Design o Mo Design 3D hasta que
la nueva versión pase pruebas de apertura, edición, exportación y restauración
de todos los proyectos. Crear respaldos independientes antes de transferir
perfiles. No copiar perfiles automáticamente.
README
echo "Source copied and renamed at $ROOT"
echo "NEXT: inspect remaining identifiers, run npm ci, npm tests and build."
echo "NOT PUSHED. Original repositories and installations remain unchanged."
