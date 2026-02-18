dotai — .NET AI profiler/debugger CLI (scaffold)

This is a minimal scaffold implementing a CLI command surface for a .NET-focused AI profiler/debugger. It includes stub implementations for `attach`, `trace record`, and `analyze` so you can iterate on command behavior and session artifact formats.

Build

dotnet build

Run examples

dotnet run -- attach --pid 12345
dotnet run -- trace record --duration 10
dotnet run -- analyze sessions/2026...-xxxxx

Status

- Build: succeeds (warnings only about TraceEvent package version and nullable annotations).
- Postprocessing: defensive reflection-based implementation is present in `Commands/PostProcessCommand.cs`.
  - Tries to convert `.nettrace` to `.etlx` via reflection (`TryForceTraceLogConvert`).
  - Attempts version-aware ETLX -> StackSource processing (`TryVersionedTraceEventPaths`) and a generic ETLX path (`TryProcessEtlx`).
  - Falls back to typed-reflection EventPipe parsing that attaches dispatch handlers and writes `trace-reflection.json`.
  - Produces artifacts registered with `SessionHelper.AddArtifact`: `summary.json`, `trace-reflection.json`, `hotspots.json`, `folded.txt` (placeholders when parsing fails).

Quick test

1) Record and postprocess a short trace (Windows PowerShell):

   .\scripts\record_and_postprocess.ps1 -Duration 5 -SessionPrefix test

2) Inspect the session directory under `sessions/<prefix-...>` and review `summary.json`, `trace-reflection.json`, `hotspots.json`, and `folded.txt`.

Where to work

- Main postprocessing logic: `Commands/PostProcessCommand.cs` (incremental, defensive edits only).
- Session helpers: `Utilities/SessionHelper.cs` (session creation and artifact registration).

Notes

- The code intentionally avoids making network calls or uploading traces — all work is local. Always register artifacts via `SessionHelper.AddArtifact` when creating outputs.
- If you want me to continue with focused tasks (improve ETLX detection, add symbol resolution, or implement pointer-based fallbacks), reply with 1–3 and I will implement the change.

Tools and symbol helpers

- To reliably process traces with the full TraceEvent/StackSource flow you should provide compatible helper assemblies. Use the included downloader to fetch specific NuGet package contents into `tools/`:

  PowerShell (Windows or pwsh):

    .\scripts\fetch_tools.ps1 -Packages @("Microsoft.Diagnostics.Tracing.TraceEvent:3.0.0","Microsoft.Diagnostics.Symbols:1.0.0")

  This places package contents in `tools/<PackageId>/<Version>/` — the postprocessor tries to probe `tools/` for compatible assemblies before falling back to textual extraction.

- If you prefer the older TraceEvent 2.x API shapes you can also fetch `Microsoft.Diagnostics.Tracing.TraceEvent:2.0.120` into `tools/` and the version-aware reflection will attempt both shapes.

Local smoke run considerations

- The smoke recording script needs permissions to attach to the target process and to write files under `sessions/`.
- On CI (Windows runners) the workflow already calls the fetch script before attempting the smoke script; the recording step may still fail if the runner blocks diagnostics or lacks permissions (this is expected and non-fatal).
