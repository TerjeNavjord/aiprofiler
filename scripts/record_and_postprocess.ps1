# PowerShell script to start the sample TestApp, record an EventPipe trace, and run postprocess
# Usage (from repository root):
#   .\scripts\record_and_postprocess.ps1 -Duration 8 -SessionPrefix automated

param(
    [int]$Duration = 30,
    [string]$SessionPrefix = "automated"
)

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$sessionId = "$SessionPrefix-$timestamp"

# Create sessions dir and session-specific directory for logs/artifacts
$sessionsRoot = Join-Path -Path (Get-Location) -ChildPath "sessions"
New-Item -Path $sessionsRoot -ItemType Directory -Force | Out-Null
$sessionDir = Join-Path $sessionsRoot $sessionId
New-Item -Path $sessionDir -ItemType Directory -Force | Out-Null


Write-Output "Building samples/TestApp..."
dotnet build "samples/TestApp" -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build samples/TestApp. Fix build errors and run script again."
    exit 1
}

# Ensure tools are copied into app output so reflection can find TraceEvent/Symbols helpers
Write-Output "Copying any downloaded tools into app build output..."
try { .\scripts\copy_tools_to_app.ps1 -Configuration Debug -Framework net10.0 } catch { Write-Output "copy_tools_to_app.ps1 failed or not present: $($_.Exception.Message)" }

Write-Output "Starting TestApp (built dll)..."
$dll = Get-ChildItem -Path "samples/TestApp/bin/Debug" -Filter "TestApp.dll" -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $dll) {
    Write-Error "Could not find TestApp.dll after build."
    exit 1
}
$dllPath = $dll.FullName
# Redirect TestApp stdout/stderr to session dir for post-mortem
$outFile = Join-Path $sessionDir "testapp.stdout.txt"
$errFile = Join-Path $sessionDir "testapp.stderr.txt"
try {
    $proc = Start-Process -FilePath "dotnet" -ArgumentList $dllPath -PassThru -RedirectStandardOutput $outFile -RedirectStandardError $errFile
} catch {
    # Fallback if RedirectStandardOutput is unsupported in this shell
    $proc = Start-Process -FilePath "dotnet" -ArgumentList $dllPath -PassThru
}
$childPid = $proc.Id
Write-Output "Started TestApp with PID $childPid (stdout-> $outFile, stderr-> $errFile)"
Start-Sleep -Seconds 2
Write-Output "Waiting for PID $childPid to be exposed to diagnostics (up to 20s)..."
$found = $false
$maxWait = 20
for ($i = 0; $i -lt $maxWait; $i++) {
    try {
        $psOut = dotnet run --project . --no-build -- ps 2>&1 | Out-String
    } catch {
        $psOut = $_.Exception.Message
    }
    # Save the latest ps output snapshot to session dir for debugging
    try { $psOut | Out-File -FilePath (Join-Path $sessionDir "ps_snapshot.txt") -Encoding utf8 } catch {}
    if ($psOut -match "\b$childPid\b") {
        Write-Output "PID $childPid is now exposed to diagnostics."
        $found = $true
        break
    }
    Start-Sleep -Seconds 1
}
if (-not $found) {
    Write-Output "PID $childPid did not appear in diagnostics list after waiting $maxWait seconds. Proceeding to attempt recording once; attach may fail."
}

Write-Output "Recording trace for $Duration seconds into session $sessionId..."
# Capture recorder output to log file for diagnosis
$recordLog = Join-Path $sessionDir "record.log"
dotnet run --project . --no-build -- trace record --pid $childPid --duration $Duration --session $sessionId *>&1 | Tee-Object -FilePath $recordLog
if ($LASTEXITCODE -ne 0) {
    Write-Output "--no-build run failed; attempting run with build (may take longer and show build errors)..."
    dotnet run --project . -- trace record --pid $childPid --duration $Duration --session $sessionId *>&1 | Tee-Object -FilePath $recordLog
}

Write-Output "Running postprocess..."
dotnet run --project . --no-build -- analyze postprocess $sessionId
if ($LASTEXITCODE -ne 0) {
    Write-Output "--no-build analyze failed; attempting run with build..."
    dotnet run --project . -- analyze postprocess $sessionId
}

Write-Output "Stopping TestApp (PID $childPid)..."
if (Get-Process -Id $childPid -ErrorAction SilentlyContinue) {
    Stop-Process -Id $childPid -Force
} else {
    Write-Output "Process $childPid already exited."
}

# Save final diagnostics
try { dotnet run --project . -- ps | Out-File -FilePath (Join-Path $sessionDir "ps_after.txt") -Encoding utf8 } catch {}

Write-Output "Done. Session: $sessionId"
Write-Output "Summary: sessions/$sessionId/summary.json"
