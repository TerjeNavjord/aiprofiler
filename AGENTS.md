Project Agents and Status

Summary

- The repo contains a CLI scaffold `dotai` that records .NET EventPipe traces and postprocesses them locally to produce artifacts useful for later analysis.

Primary agent responsibilities

- PostProcess agent (Commands/PostProcessCommand.cs):
  - Convert `.nettrace` to `.etlx` using reflection-based probes (`TryForceTraceLogConvert`).
  - Attempt version-aware ETLX -> StackSource processing when possible (`TryVersionedTraceEventPaths`).
  - Fall back to typed-reflection parsing by instantiating EventPipe/TraceEvent sources and attaching dynamic dispatch handlers.
  - Always write `summary.json` and record artifacts using `SessionHelper.AddArtifact`.

Current status

- Build: successful with warnings about TraceEvent package resolution and nullable annotations.
- ETLX path: added heuristics and version-aware reflection to try both TraceLog instance methods and static Open/OpenOrConvert helpers.
- StackSource processing: best-effort via reflection; attempts to invoke helper types like SampleProfiler/ThreadTimeComputer.
- Fallbacks: textual extraction of stacks and lightweight folded/hotspot artifacts when richer APIs are unavailable.

Next recommended work

1) Add precise, version-targeted reflection for TraceEvent 2.x and 3.x API shapes to reliably produce ETLX and StackSource artifacts. (High impact)
2) Implement symbol resolution using Microsoft.Diagnostics.Symbols or TraceEvent symbol helpers when StackSource is available. (Medium)
3) Implement raw pointer extraction from `ClrThreadSample` objects as a fallback path to resolve stacks when ETLX/StackSource is unavailable. (Medium)

How to test locally

- Use `scripts/record_and_postprocess.ps1` to record a short trace and run postprocessing.
- Inspect `sessions/<sessionId>/summary.json` and the registered artifacts under `sessions/<sessionId>/`.

Contact

- If an agent should take the next step, open an issue or assign the task specifying which option above to implement next.
