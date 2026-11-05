param(
 [Parameter(Mandatory=$true)][string]$Frames,
 [Parameter(Mandatory=$true)][string]$Output,
 [string]$Ffmpeg = 'ffmpeg'
)
$ErrorActionPreference = 'Stop'
for ($i=0; $i -lt 60; $i++) {
 $frame = Join-Path $Frames ('frame-{0:D4}.png' -f $i)
 if (-not (Test-Path -LiteralPath $frame)) { throw "Missing rendered frame: $frame" }
}
if (Test-Path -LiteralPath $Output) { throw 'Output already exists. Choose a new output filename.' }
& $Ffmpeg -hide_banner -framerate 6 -i (Join-Path $Frames 'frame-%04d.png') -vf 'minterpolate=fps=30:mi_mode=mci:mc_mode=aobmc:me_mode=bidir,format=yuv420p' -c:v libx264 -preset slow -crf 17 -movflags +faststart -an $Output
if ($LASTEXITCODE -ne 0) { throw "Video encoding failed: $LASTEXITCODE" }
