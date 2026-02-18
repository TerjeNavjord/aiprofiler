<#
Copies DLLs from tools/ into the build output folder so PostProcess reflection finds them via Assembly.LoadFrom.
Usage:
  .\scripts\copy_tools_to_app.ps1 -Configuration Debug
#>
param(
    [string]$Configuration = "Debug",
    [string]$Framework = "net10.0"
)

function Write-Info($s) { Write-Host "[copy-tools] $s" }

$buildOut = Join-Path -Path (Get-Location) -ChildPath "bin\$Configuration\$Framework"
if (-not (Test-Path $buildOut)) { Write-Info "Build output not found: $buildOut"; exit 1 }

$tools = Join-Path (Get-Location) "tools"
if (-not (Test-Path $tools)) { Write-Info "No tools/ directory found; nothing to copy."; exit 0 }

Get-ChildItem -Path $tools -Recurse -Include '*.dll' | ForEach-Object {
    try {
        $dest = Join-Path $buildOut $_.Name
        Copy-Item -Path $_.FullName -Destination $dest -Force
        Write-Info "Copied $($_.FullName) -> $dest"
    } catch {
        Write-Info "Failed to copy $($_.FullName): $($_.Exception.Message)"
    }
}

Write-Info "Done copying tools to $buildOut"
