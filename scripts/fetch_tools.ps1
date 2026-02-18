<#
.SYNOPSIS
    Download specified NuGet packages and extract DLLs into the repository `tools/` folder.

.DESCRIPTION
    This script fetches package nupkgs from nuget.org (v3 flat container), extracts their
    contents and places them under `./tools/<packageId>/<version>/`. The PostProcess code
    already attempts to load symbol and TraceEvent helper assemblies from `tools/`.

.EXAMPLE
    # Use defaults (TraceEvent 2.0.120, TraceEvent 3.0.0, Microsoft.Diagnostics.Symbols 1.0.0)
    .\scripts\fetch_tools.ps1

    # Specify explicit versions
    .\scripts\fetch_tools.ps1 -Packages "Microsoft.Diagnostics.Tracing.TraceEvent:2.0.120","Microsoft.Diagnostics.Symbols:1.0.0"
#>

param(
    [string[]]
    $Packages = @(
        "Microsoft.Diagnostics.Tracing.TraceEvent:2.0.120",
        "Microsoft.Diagnostics.Tracing.TraceEvent:3.0.0",
        "Microsoft.Diagnostics.Symbols:1.0.0"
    ),
    [string]
    $ToolsRoot = "tools"
)

function Write-Info($s) { Write-Host "[fetch-tools] $s" }

if (-not (Test-Path $ToolsRoot)) { New-Item -Path $ToolsRoot -ItemType Directory | Out-Null }

foreach ($p in $Packages)
{
    try {
        $parts = $p -split ':'
        if ($parts.Length -ne 2) { Write-Info "Skipping invalid package spec: $p"; continue }
        $id = $parts[0]
        $ver = $parts[1]
        $idLower = $id.ToLowerInvariant()
        $url = "https://api.nuget.org/v3-flatcontainer/$idLower/$ver/$idLower.$ver.nupkg"
        Write-Info "Downloading $id $ver from $url"

        $tmp = [System.IO.Path]::GetTempFileName()
        Remove-Item $tmp
        $tmp = "$tmp.nupkg"

        Invoke-WebRequest -Uri $url -OutFile $tmp -UseBasicParsing -ErrorAction Stop

        $dest = Join-Path -Path $ToolsRoot -ChildPath "$id\$ver"
        if (-not (Test-Path $dest)) { New-Item -Path $dest -ItemType Directory -Force | Out-Null }

        Write-Info "Extracting to $dest"
        Expand-Archive -Path $tmp -DestinationPath $dest -Force

        # enumerate DLLs under lib/ and runtimes/
        $dlls = Get-ChildItem -Path $dest -Recurse -Include '*.dll' -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match '\\lib\\|\\runtimes\\|\\ref\\' }
        if ($dlls.Count -eq 0) {
            # fallback: any dlls
            $dlls = Get-ChildItem -Path $dest -Recurse -Include '*.dll' -ErrorAction SilentlyContinue
        }

        if ($dlls.Count -gt 0)
        {
            Write-Info "Found assemblies:"
            foreach ($d in $dlls) { Write-Host " - $($d.FullName)" }
        }
        else { Write-Info "No DLLs found in package $id $ver (this may be a symbols/source package)." }

        Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    }
    catch {
        $msg = $_.Exception.Message -replace '"','\"'
        Write-Host ([String]::Format("[fetch-tools] Failed to fetch {0}: {1}", $p, $msg)) -ForegroundColor Yellow
    }
}

Write-Info "Done. Tools are under: $ToolsRoot"
