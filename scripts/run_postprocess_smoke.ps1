param(
    [string]$SessionId
)

if (-not $SessionId) {
    Write-Error "Usage: .\scripts\run_postprocess_smoke.ps1 <sessionId>"
    exit 1
}

Write-Output "Running postprocess for session: $SessionId"
# Copy any downloaded tools into app build output so reflection can find helpers
try { .\scripts\copy_tools_to_app.ps1 -Configuration Debug -Framework net10.0 -ErrorAction SilentlyContinue } catch { }
dotnet run --project . -- analyze postprocess $SessionId

$dir = Join-Path (Get-Location) "sessions\$SessionId"
if (Test-Path (Join-Path $dir "summary.json")) {
    Write-Output "Summary: " (Get-Content (Join-Path $dir "summary.json"))
} else {
    Write-Output "No summary.json produced. Listing session dir:"
    Get-ChildItem $dir | Format-List
}
